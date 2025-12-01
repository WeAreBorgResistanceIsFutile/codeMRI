using System;
using System.Collections.Generic;
using System.Linq;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;

namespace codeMRI.Core.Services
{
    public class ArchitecturalPatternService : IArchitecturalPatternService
    {
        public ArchitecturalPattern RecognizePattern(ModuleNode module, EnhancedDependencyGraph graph)
        {
            var pattern = new ArchitecturalPattern
            {
                Type = ArchitecturalPatternType.Unknown,
                Name = "Unknown",
                Confidence = 0.0
            };

            // Get all nodes in the module
            var nodes = new List<GraphNode>();
            foreach (var componentId in module.Components)
            {
                var node = graph.GetNode(componentId);
                if (node != null)
                {
                    nodes.Add(node);
                }
            }

            if (nodes.Count == 0) return pattern;

            // Check for Layered Architecture
            var layeredScore = CalculateLayeredScore(nodes, graph);
            if (layeredScore > 0.6)
            {
                return new ArchitecturalPattern
                {
                    Type = ArchitecturalPatternType.Layered,
                    Name = "Layered Architecture",
                    Confidence = layeredScore
                };
            }

            // Check for MVC
            var mvcScore = CalculateMVCScore(nodes);
            if (mvcScore > 0.6 && mvcScore > layeredScore)
            {
                return new ArchitecturalPattern
                {
                    Type = ArchitecturalPatternType.MVC,
                    Name = "Model-View-Controller",
                    Confidence = mvcScore
                };
            }

            return pattern;
        }

        public ArchitecturalLayerType DetermineLayer(GraphNode node)
        {
            var name = node.ComponentId;
            var type = node.Metadata.Type;

            if (ContainsAny(name, type, "Controller", "View", "Page", "DTO", "ViewModel", "Presenter"))
                return ArchitecturalLayerType.Presentation;

            if (ContainsAny(name, type, "Service", "Manager", "Handler", "UseCase", "Command", "Query"))
                return ArchitecturalLayerType.Application;

            if (ContainsAny(name, type, "Entity", "Domain", "ValueObject", "Aggregate"))
                return ArchitecturalLayerType.Domain;

            if (ContainsAny(name, type, "Repository", "DbContext", "Dao"))
                return ArchitecturalLayerType.Data;

            if (ContainsAny(name, type, "Gateway", "Client"))
                return ArchitecturalLayerType.Infrastructure;
            
            if (ContainsAny(name, type, "Util", "Helper", "Extensions", "Common"))
                return ArchitecturalLayerType.CrossCutting;

            return ArchitecturalLayerType.Unknown;
        }

        private bool ContainsAny(string name, string type, params string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase) || 
                    type.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private double CalculateLayeredScore(List<GraphNode> nodes, EnhancedDependencyGraph graph)
        {
            // Check if dependencies flow in correct direction (Presentation -> Application -> Domain/Infrastructure)
            // And typically Infrastructure -> Domain (Dependency Inversion) or Application -> Infrastructure (traditional)
            
            int correctFlows = 0;
            int totalFlows = 0;

            foreach (var node in nodes)
            {
                var sourceLayer = DetermineLayer(node);
                if (sourceLayer == ArchitecturalLayerType.Unknown) continue;

                foreach (var targetId in node.OutEdges)
                {
                    // Only consider internal dependencies within the module
                    if (!nodes.Any(n => n.ComponentId == targetId)) continue;

                    var targetNode = graph.GetNode(targetId);
                    if (targetNode == null) continue;

                    var targetLayer = DetermineLayer(targetNode);
                    if (targetLayer == ArchitecturalLayerType.Unknown) continue;
                    if (sourceLayer == targetLayer) continue; // Same layer calls are neutral

                    totalFlows++;
                    if (IsValidLayerFlow(sourceLayer, targetLayer))
                    {
                        correctFlows++;
                    }
                }
            }

            if (totalFlows == 0) return 0.0;
            
            // Also check if we have representation from multiple layers
            var layersPresent = nodes.Select(DetermineLayer).Where(l => l != ArchitecturalLayerType.Unknown).Distinct().Count();
            if (layersPresent < 2) return 0.0;

            return (double)correctFlows / totalFlows;
        }

        private bool IsValidLayerFlow(ArchitecturalLayerType source, ArchitecturalLayerType target)
        {
            // Traditional Layered: Presentation -> Application -> Domain/Data
            // Strict Layered: Presentation -> Application, Application -> Domain, etc.
            
            switch (source)
            {
                case ArchitecturalLayerType.Presentation:
                    return target == ArchitecturalLayerType.Application || target == ArchitecturalLayerType.Domain; // Relaxed
                case ArchitecturalLayerType.Application:
                    return target == ArchitecturalLayerType.Domain || target == ArchitecturalLayerType.Infrastructure || target == ArchitecturalLayerType.Data;
                case ArchitecturalLayerType.Infrastructure:
                    // In Clean Architecture, Infra depends on Domain. In traditional, App depends on Infra.
                    // Let's assume correct flow is "Downwards" or "Towards Domain"
                    return target == ArchitecturalLayerType.Domain || target == ArchitecturalLayerType.Data;
                case ArchitecturalLayerType.Domain:
                    return false; // Domain should not depend on outer layers
                default:
                    return false;
            }
        }

        private double CalculateMVCScore(List<GraphNode> nodes)
        {
            int models = 0, views = 0, controllers = 0;
            
            foreach(var node in nodes)
            {
                if (ContainsAny(node.ComponentId, node.Metadata.Type, "Model")) models++;
                if (ContainsAny(node.ComponentId, node.Metadata.Type, "View", "Page")) views++;
                if (ContainsAny(node.ComponentId, node.Metadata.Type, "Controller")) controllers++;
            }

            if (models > 0 && views > 0 && controllers > 0)
                return 0.8;
            if ((models > 0 && controllers > 0) || (views > 0 && controllers > 0))
                return 0.5;
            
            return 0.0;
        }
    }
}
