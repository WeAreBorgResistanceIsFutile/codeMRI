namespace codeMRI.Visualization.Models;

public class VisualArtifacts
{
    public string ArchitectureDiagram { get; set; } = string.Empty;
    public Dictionary<string, string> ComponentDiagrams { get; set; } = new();
    public Dictionary<string, string> SequenceDiagrams { get; set; } = new();
}