using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface IReferenceManagementService
{
    /// <summary>
    /// Registers a component in the global registry.
    /// </summary>
    /// <param name="id">Unique identifier for the component.</param>
    /// <param name="name">Human-readable name of the component.</param>
    /// <param name="filePath">Path to the source file.</param>
    /// <param name="docPath">Path to the documentation file (relative or absolute).</param>
    void RegisterComponent(string id, string name, string filePath, string docPath);

    /// <summary>
    /// Registers a relationship between two components.
    /// </summary>
    /// <param name="sourceId">ID of the source component.</param>
    /// <param name="targetId">ID of the target component.</param>
    /// <param name="type">Type of dependency/relationship.</param>
    void RegisterRelationship(string sourceId, string targetId, EdgeType type);

    /// <summary>
    /// Finds potential references to other components within the provided text content.
    /// </summary>
    /// <param name="content">The text to analyze.</param>
    /// <param name="sourceComponentId">The ID of the component containing the text (to exclude self-references).</param>
    /// <returns>A collection of detected cross-references.</returns>
    IEnumerable<CrossReference> FindReferences(string content, string sourceComponentId);

    /// <summary>
    /// Resolves the documentation link for a given component ID.
    /// </summary>
    /// <param name="targetComponentId">The ID of the target component.</param>
    /// <returns>The documentation path/link, or null if not found.</returns>
    string? ResolveLink(string targetComponentId);

    /// <summary>
    /// Enriches the provided content by replacing component names with hyperlinks to their documentation.
    /// </summary>
    /// <param name="content">The original text content.</param>
    /// <param name="sourceComponentId">The ID of the component (context) to avoid self-linking.</param>
    /// <returns>The content with hyperlinks.</returns>
    string EnrichContentWithLinks(string content, string sourceComponentId);
    
    /// <summary>
    /// Gets a component by its ID.
    /// </summary>
    /// <param name="id">The component ID.</param>
    /// <returns>The registered component info, or null.</returns>
    RegisteredComponent? GetComponent(string id);
}
