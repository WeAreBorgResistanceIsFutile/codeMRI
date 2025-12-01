using System.Collections.Generic;

namespace codeMRI.Core.Models
{
    public enum ArchitecturalPatternType
    {
        Layered,
        Microservices,
        EventDriven,
        MVC,
        MVVM,
        CleanArchitecture,
        RepositoryPattern,
        Pipeline,
        Unknown
    }

    public class ArchitecturalPattern
    {
        public ArchitecturalPatternType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
