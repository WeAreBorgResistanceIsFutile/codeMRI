namespace codeMRI.Core.Models
{
    /// <summary>
    /// Represents an architectural layer in the system
    /// </summary>
    public enum ArchitecturalLayerType
    {
        Presentation,
        Application,
        Domain,
        Infrastructure,
        Data,
        CrossCutting,
        Unknown
    }

    /// <summary>
    /// Represents a specific architectural layer definition
    /// </summary>
    public class ArchitecturalLayer
    {
        public string Name { get; set; } = string.Empty;
        public ArchitecturalLayerType Type { get; set; }
        public int Order { get; set; }
        public string Description { get; set; } = string.Empty;
        public HashSet<string> AllowedDependencies { get; set; } = new HashSet<string>();
    }
}
