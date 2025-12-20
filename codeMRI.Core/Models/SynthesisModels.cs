namespace codeMRI.Core.Models;

/// <summary>
/// Entities extracted from documentation for anchoring during synthesis.
/// These identifiers MUST be preserved when summarizing content.
/// </summary>
public class ExtractedEntities
{
    /// <summary>
    /// Class names found in the documentation (e.g., "PaymentService", "OrderRepository")
    /// </summary>
    public List<string> ClassNames { get; set; } = new();
    
    /// <summary>
    /// Function/method names found in the documentation
    /// </summary>
    public List<string> FunctionNames { get; set; } = new();
    
    /// <summary>
    /// Architectural pattern names (e.g., "Repository", "Factory", "CQRS")
    /// </summary>
    public List<string> PatternNames { get; set; } = new();
    
    /// <summary>
    /// External dependency names (e.g., "Stripe", "PostgreSQL", "Redis")
    /// </summary>
    public List<string> DependencyNames { get; set; } = new();
    
    /// <summary>
    /// Combine all entities into a single list for prompt inclusion
    /// </summary>
    public IEnumerable<string> AllEntities => 
        ClassNames.Concat(FunctionNames).Concat(PatternNames).Concat(DependencyNames);
    
    /// <summary>
    /// Returns true if any entities were extracted
    /// </summary>
    public bool HasEntities => AllEntities.Any();
}

/// <summary>
/// A compressed summary of a module's documentation.
/// Used in Map-Reduce processing to reduce context size.
/// </summary>
public class ModuleSummary
{
    public string ModuleId { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    
    /// <summary>
    /// Core purpose in 1-2 sentences
    /// </summary>
    public string CorePurpose { get; set; } = string.Empty;
    
    /// <summary>
    /// Key functions/capabilities (entity-anchored, max 5)
    /// </summary>
    public List<string> KeyFunctions { get; set; } = new();
    
    /// <summary>
    /// Dependencies on other modules
    /// </summary>
    public List<string> Dependencies { get; set; } = new();
    
    /// <summary>
    /// Detected architectural pattern
    /// </summary>
    public string ArchitecturalPattern { get; set; } = "Not identified";
    
    /// <summary>
    /// Original character count (for compression ratio calculation)
    /// </summary>
    public int OriginalCharCount { get; set; }
}

/// <summary>
/// Synthesis strategy options for parent page generation
/// </summary>
public enum SynthesisStrategy
{
    /// <summary>
    /// Current default: Send all child page content directly
    /// </summary>
    Direct,
    
    /// <summary>
    /// Extract and anchor key entities before synthesis
    /// </summary>
    WithEntityAnchoring,
    
    /// <summary>
    /// Summarize children first (Map), then synthesize (Reduce)
    /// </summary>
    MapReduce
}
