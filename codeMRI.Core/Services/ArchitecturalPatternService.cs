using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;

namespace codeMRI.Core.Services;

public class ArchitecturalPatternService : IArchitecturalPatternService
{
    public ArchitecturalPattern RecognizePattern(ModuleNode module, EnhancedDependencyGraph graph)
    {
        var nodes = GetModuleNodes(module, graph);
        if (nodes.Count == 0) return new ArchitecturalPattern { Type = ArchitecturalPatternType.Unknown };

        // Check patterns in order of specificity

        // 1. Microservices (High confidence if detected)
        if (IsMicroservices(nodes, graph, out var msConfidence))
            return new ArchitecturalPattern
            {
                Type = ArchitecturalPatternType.Microservices,
                Name = "Microservices",
                Confidence = msConfidence
            };

        // 2. Event-Driven
        if (IsEventDriven(nodes, graph, out var edConfidence))
            return new ArchitecturalPattern
            {
                Type = ArchitecturalPatternType.EventDriven,
                Name = "Event-Driven",
                Confidence = edConfidence
            };

        // 3. Clean Architecture / Hexagonal
        if (IsCleanArchitecture(nodes, graph, out var caConfidence))
            return new ArchitecturalPattern
            {
                Type = ArchitecturalPatternType.CleanArchitecture,
                Name = "Clean Architecture",
                Confidence = caConfidence
            };

        // 4. MVC
        if (IsMVC(nodes, out var mvcConfidence))
            return new ArchitecturalPattern
            {
                Type = ArchitecturalPatternType.MVC,
                Name = "Model-View-Controller",
                Confidence = mvcConfidence
            };

        // 5. Layered (General fallback)
        if (IsLayeredArchitecture(nodes, graph, out var layeredConfidence))
            return new ArchitecturalPattern
            {
                Type = ArchitecturalPatternType.Layered,
                Name = "Layered Architecture",
                Confidence = layeredConfidence
            };

        return new ArchitecturalPattern
        {
            Type = ArchitecturalPatternType.Unknown,
            Name = "Unknown",
            Confidence = 0.0
        };
    }

    public ArchitecturalLayerType DetermineLayer(GraphNode node)
    {
        var name = node.ComponentId;
        var type = node.Metadata.Type;

        // Language-agnostic & Language-specific heuristics

        // Presentation / UI
        if (ContainsAny(name, type, "Controller", "View", "Page", "DTO", "ViewModel", "Presenter", "Component",
                "Screen", "Route"))
            return ArchitecturalLayerType.Presentation;

        // Application / Business Logic
        if (ContainsAny(name, type, "Service", "Manager", "Handler", "UseCase", "Command", "Query", "Workflow",
                "Orchestrator"))
            return ArchitecturalLayerType.Application;

        // Domain / Core
        if (ContainsAny(name, type, "Entity", "Domain", "ValueObject", "Aggregate", "Model", "Core", "Shared",
                "Interfaces"))
            return ArchitecturalLayerType.Domain;

        // Data / Infrastructure
        if (ContainsAny(name, type, "Repository", "DbContext", "Dao", "Storage", "Cache", "Database", "Sql", "Mongo",
                "Redis"))
            return ArchitecturalLayerType.Data;

        // Infrastructure / External
        if (ContainsAny(name, type, "Gateway", "Client", "Adapter", "Proxy", "External", "Infra", "Config"))
            return ArchitecturalLayerType.Infrastructure;

        // Cross-Cutting
        if (ContainsAny(name, type, "Util", "Helper", "Extensions", "Common", "Logging", "Security", "Auth",
                "Exception"))
            return ArchitecturalLayerType.CrossCutting;

        return ArchitecturalLayerType.Unknown;
    }

    private List<GraphNode> GetModuleNodes(ModuleNode module, EnhancedDependencyGraph graph)
    {
        var nodes = new List<GraphNode>();
        if (module?.Components == null) return nodes;

        foreach (var componentId in module.Components)
        {
            var node = graph.GetNode(componentId);
            if (node != null) nodes.Add(node);
        }

        return nodes;
    }

    private bool IsMicroservices(List<GraphNode> nodes, EnhancedDependencyGraph graph, out double confidence)
    {
        confidence = 0.0;
        // Heuristic: Multiple independent "Service" entry points with little direct coupling
        var serviceNodes = nodes.Where(n =>
            ContainsAny(n.ComponentId, n.Metadata.Type, "Service", "API", "Microservice") &&
            !ContainsAny(n.ComponentId, n.Metadata.Type, "ApplicationService", "DomainService")
        ).ToList();

        if (serviceNodes.Count < 2) return false;

        var linksBetweenServices = 0;
        foreach (var s1 in serviceNodes)
        foreach (var s2 in serviceNodes)
        {
            if (s1 == s2) continue;
            if (s1.OutEdges.Contains(s2.ComponentId)) linksBetweenServices++;
        }

        var connectivity = (double)linksBetweenServices / (serviceNodes.Count * (serviceNodes.Count - 1));

        if (connectivity < 0.2) // Very low coupling
        {
            confidence = 0.8;
            return true;
        }

        if (connectivity < 0.4)
        {
            confidence = 0.5;
            return true;
        }

        return false;
    }

    private bool IsEventDriven(List<GraphNode> nodes, EnhancedDependencyGraph graph, out double confidence)
    {
        confidence = 0.0;
        var eventComponents = 0;
        var eventLinks = 0;

        foreach (var node in nodes)
            if (ContainsAny(node.ComponentId, node.Metadata.Type, "Event", "Message", "Bus", "Queue", "Topic",
                    "Publisher", "Subscriber", "Handler", "Listener"))
            {
                eventComponents++;
                foreach (var neighborId in node.OutEdges)
                {
                    var neighbor = graph.GetNode(neighborId);
                    if (neighbor != null && ContainsAny(neighbor.ComponentId, neighbor.Metadata.Type, "Event",
                            "Message", "Handler"))
                        eventLinks++;
                }
            }

        if (nodes.Count == 0) return false;

        var ratio = (double)eventComponents / nodes.Count;
        if (ratio > 0.25 || (eventComponents >= 3 && eventLinks >= 1))
        {
            confidence = Math.Min(0.9, 0.5 + ratio);
            return true;
        }

        return false;
    }

    private bool IsCleanArchitecture(List<GraphNode> nodes, EnhancedDependencyGraph graph, out double confidence)
    {
        confidence = 0.0;
        // Check for concentric layers: Domain <- Application <- Infrastructure/Presentation
        // Key indicator: Domain depends on NOTHING (or only Utils)

        var domainNodes = nodes.Where(n => DetermineLayer(n) == ArchitecturalLayerType.Domain).ToList();
        if (domainNodes.Count == 0) return false;

        var domainViolations = 0;
        foreach (var domainNode in domainNodes)
        foreach (var targetId in domainNode.OutEdges)
            // Check if target is inside module but outside domain
            if (nodes.Any(n => n.ComponentId == targetId))
            {
                var targetNode = graph.GetNode(targetId);
                if (targetNode != null)
                {
                    var layer = DetermineLayer(targetNode);
                    if (layer != ArchitecturalLayerType.Domain && layer != ArchitecturalLayerType.CrossCutting &&
                        layer != ArchitecturalLayerType.Unknown) domainViolations++;
                }
            }

        if (domainViolations == 0 && domainNodes.Count > 0)
        {
            confidence = 0.85;
            return true;
        }

        return false;
    }

    private bool IsMVC(List<GraphNode> nodes, out double confidence)
    {
        confidence = 0.0;
        int models = 0, views = 0, controllers = 0;

        foreach (var node in nodes)
        {
            if (ContainsAny(node.ComponentId, node.Metadata.Type, "Model")) models++;
            if (ContainsAny(node.ComponentId, node.Metadata.Type, "View", "Page", "Razor")) views++;
            if (ContainsAny(node.ComponentId, node.Metadata.Type, "Controller")) controllers++;
        }

        if (models > 0 && views > 0 && controllers > 0)
        {
            confidence = 0.9;
            return true;
        }

        if ((models > 0 && controllers > 0) || (views > 0 && controllers > 0))
        {
            confidence = 0.6;
            return true;
        }

        return false;
    }

    private bool IsLayeredArchitecture(List<GraphNode> nodes, EnhancedDependencyGraph graph, out double confidence)
    {
        confidence = 0.0;
        var correctFlows = 0;
        var totalFlows = 0;

        foreach (var node in nodes)
        {
            var sourceLayer = DetermineLayer(node);
            if (sourceLayer == ArchitecturalLayerType.Unknown) continue;

            foreach (var targetId in node.OutEdges)
            {
                if (!nodes.Any(n => n.ComponentId == targetId)) continue;

                var targetNode = graph.GetNode(targetId);
                if (targetNode == null) continue;

                var targetLayer = DetermineLayer(targetNode);
                if (targetLayer == ArchitecturalLayerType.Unknown) continue;
                if (sourceLayer == targetLayer) continue;

                totalFlows++;
                if (IsValidLayerFlow(sourceLayer, targetLayer)) correctFlows++;
            }
        }

        if (totalFlows == 0) return false;

        var layersPresent = nodes.Select(DetermineLayer).Where(l => l != ArchitecturalLayerType.Unknown).Distinct()
            .Count();
        if (layersPresent < 2) return false;

        confidence = (double)correctFlows / totalFlows;
        return confidence > 0.5;
    }

    private bool IsValidLayerFlow(ArchitecturalLayerType source, ArchitecturalLayerType target)
    {
        switch (source)
        {
            case ArchitecturalLayerType.Presentation:
                return target == ArchitecturalLayerType.Application || target == ArchitecturalLayerType.Domain;
            case ArchitecturalLayerType.Application:
                return target == ArchitecturalLayerType.Domain || target == ArchitecturalLayerType.Infrastructure ||
                       target == ArchitecturalLayerType.Data;
            case ArchitecturalLayerType.Infrastructure:
                return target == ArchitecturalLayerType.Domain || target == ArchitecturalLayerType.Data;
            case ArchitecturalLayerType.Domain:
                return false;
            default:
                return false;
        }
    }

    private bool ContainsAny(string name, string type, params string[] keywords)
    {
        foreach (var keyword in keywords)
            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                type.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}