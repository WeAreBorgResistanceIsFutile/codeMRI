namespace codeMRI.Core.Models;

public class WikiStructure
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<WikiSection> Sections { get; set; } = new();
    public List<WikiPage> Pages { get; set; } = new();
    
    /// <summary>
    ///     Maps module IDs to section IDs for content generation linking
    /// </summary>
    public Dictionary<string, string> ModuleToSectionMap { get; set; } = new();
    public string RepoPath { get; set; } = string.Empty;
}

public class WikiSection
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> PageRefs { get; set; } = new();
    public List<WikiSection> SubSections { get; set; } = new();
    
    /// <summary>
    ///     Module IDs whose content belongs in this section
    /// </summary>
    public List<string> ModuleIds { get; set; } = new();
}