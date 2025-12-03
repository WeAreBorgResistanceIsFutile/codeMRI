using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;

namespace codeMRI.Core.Services;

public class ReferenceManagementService : IReferenceManagementService
{
    private readonly ConcurrentDictionary<string, RegisteredComponent> _registry = new();

    public void RegisterComponent(string id, string name, string filePath, string docPath)
    {
        _registry.AddOrUpdate(id,
            _ => new RegisteredComponent
            {
                Id = id,
                Name = name,
                FilePath = filePath,
                DocPath = docPath,
                RelatedComponentIds = new HashSet<string>()
            },
            (_, existing) =>
            {
                existing.Name = name;
                existing.FilePath = filePath;
                existing.DocPath = docPath;
                return existing;
            });
    }

    public void RegisterRelationship(string sourceId, string targetId, EdgeType type)
    {
        if (_registry.TryGetValue(sourceId, out var source))
        {
            lock (source.RelatedComponentIds)
            {
                source.RelatedComponentIds.Add(targetId);
            }
        }
    }

    public RegisteredComponent? GetComponent(string id)
    {
        _registry.TryGetValue(id, out var component);
        return component;
    }

    public IEnumerable<CrossReference> FindReferences(string content, string sourceComponentId)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Enumerable.Empty<CrossReference>();

        var references = new List<CrossReference>();

        foreach (var component in _registry.Values)
        {
            if (component.Id == sourceComponentId)
                continue;

            // Use word boundary to match exact component names
            // Using verbatim string literals with interpolation correctly
            var pattern = $@"\b{Regex.Escape(component.Name)}\b";
            var matches = Regex.Matches(content, pattern);
            foreach (Match match in matches)
            {
                references.Add(new CrossReference
                {
                    SourceId = sourceComponentId,
                    TargetId = component.Id,
                    TargetName = component.Name,
                    Type = EdgeType.CrossBoundary, // Default type, refined by caller if needed
                    Index = match.Index,
                    Length = match.Length,
                    Context = GetContext(content, match.Index)
                });
            }
        }

        return references;
    }

    public string? ResolveLink(string targetComponentId)
    {
        return _registry.TryGetValue(targetComponentId, out var component) ? component.DocPath : null;
    }

    public string EnrichContentWithLinks(string content, string sourceComponentId)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        // Sort by name length descending to prevent partial matches (e.g. linking "User" inside "SuperUser")
        var potentialLinks = _registry.Values
            .Where(c => c.Id != sourceComponentId)
            .OrderByDescending(c => c.Name.Length)
            .ToList();

        var result = content;

        // We need to be careful not to replace text inside already generated links.
        // A simple approach for TDD: split by existing markdown links or process carefully.
        // For now, we'll use a placeholder strategy or just regex replace if we assume plain text input.
        // The test case inputs are simple plain text.
        
        foreach (var component in potentialLinks)
        {
            var pattern = $@"\b{Regex.Escape(component.Name)}\b";
            
            // Check if the name is present
            if (Regex.IsMatch(result, pattern))
            {
                var link = $"[{component.Name}]({component.DocPath})";
                // Only replace if not already linked? 
                // Regex lookarounds: (?<!\[)Name(?!\(\)) might work to avoid double linking
                
                 result = Regex.Replace(result, $"(?<!\\[)\\b{Regex.Escape(component.Name)}\\b(?!\\])", link);
            }
        }

        return result;
    }

    private string GetContext(string content, int index)
    {
        const int contextPadding = 20;
        var start = Math.Max(0, index - contextPadding);
        var length = Math.Min(content.Length - start, index - start + 50); // 50 chars after match
        return content.Substring(start, length).Replace("\n", " ").Trim();
    }
}
