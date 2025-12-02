using codeMRI.Shared.Models;

namespace codeMRI.Visualization.Models
{
    /// <summary>
    /// Represents an interactive diagram with zoom, filtering, and export capabilities
    /// </summary>
    public class InteractiveDiagram
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string MermaidContent { get; set; } = string.Empty;
        public DiagramType Type { get; set; }
        public DiagramOptions Options { get; set; } = new();
        public List<DiagramComponent> Components { get; set; } = new();
        public List<DiagramRelationship> Relationships { get; set; } = new();
        public ExportOptions ExportOptions { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Configuration options for diagram rendering and interaction
    /// </summary>
    public class DiagramOptions
    {
        public bool EnableZoom { get; set; } = true;
        public bool EnableFiltering { get; set; } = true;
        public bool EnableExport { get; set; } = true;
        public ZoomOptions Zoom { get; set; } = new();
        public FilterOptions Filter { get; set; } = new();
        public string Theme { get; set; } = "default";
        public int MaxDepth { get; set; } = 3;
        public bool ShowLabels { get; set; } = true;
        public bool ShowMetadata { get; set; } = false;
    }

    /// <summary>
    /// Zoom configuration for interactive diagrams
    /// </summary>
    public class ZoomOptions
    {
        public double MinZoom { get; set; } = 0.1;
        public double MaxZoom { get; set; } = 5.0;
        public double DefaultZoom { get; set; } = 1.0;
        public bool EnableMouseWheel { get; set; } = true;
        public bool EnablePan { get; set; } = true;
        public bool FitToView { get; set; } = true;
    }

    /// <summary>
    /// Filtering options for diagram components and relationships
    /// </summary>
    public class FilterOptions
    {
        public List<string> IncludedComponents { get; set; } = new();
        public List<string> ExcludedComponents { get; set; } = new();
        public List<EdgeType> IncludedRelationshipTypes { get; set; } = new();
        public List<EdgeType> ExcludedRelationshipTypes { get; set; } = new();
        public List<string> IncludedLayers { get; set; } = new();
        public List<string> ExcludedLayers { get; set; } = new();
        public int MinComplexity { get; set; } = 0;
        public int MaxComplexity { get; set; } = int.MaxValue;
        public bool ShowOnlyPublic { get; set; } = false;
        public bool ShowOnlyDocumented { get; set; } = false;
    }

    /// <summary>
    /// Export options for different formats
    /// </summary>
    public class ExportOptions
    {
        public bool EnablePng { get; set; } = true;
        public bool EnableSvg { get; set; } = true;
        public bool EnablePdf { get; set; } = false;
        public PngExportOptions Png { get; set; } = new();
        public SvgExportOptions Svg { get; set; } = new();
        public PdfExportOptions Pdf { get; set; } = new();
    }

    /// <summary>
    /// PNG export configuration
    /// </summary>
    public class PngExportOptions
    {
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public int Quality { get; set; } = 90;
        public string BackgroundColor { get; set; } = "#ffffff";
        public bool Transparent { get; set; } = false;
    }

    /// <summary>
    /// SVG export configuration
    /// </summary>
    public class SvgExportOptions
    {
        public bool IncludeStyles { get; set; } = true;
        public bool IncludeMetadata { get; set; } = true;
        public bool Compressed { get; set; } = false;
        public string BackgroundColor { get; set; } = "transparent";
    }

    /// <summary>
    /// PDF export configuration
    /// </summary>
    public class PdfExportOptions
    {
        public string PaperSize { get; set; } = "A4";
        public string Orientation { get; set; } = "portrait";
        public int Margin { get; set; } = 20;
        public bool IncludeBookmarks { get; set; } = false;
    }

    /// <summary>
    /// Represents a component in the interactive diagram
    /// </summary>
    public class DiagramComponent
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Layer { get; set; } = string.Empty;
        public int Complexity { get; set; }
        public bool IsPublic { get; set; }
        public bool HasDocumentation { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool IsVisible { get; set; } = true;
        public bool IsFiltered { get; set; } = false;
    }

    /// <summary>
    /// Represents a relationship between components in the interactive diagram
    /// </summary>
    public class DiagramRelationship
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FromComponent { get; set; } = string.Empty;
        public string ToComponent { get; set; } = string.Empty;
        public EdgeType Type { get; set; }
        public double Strength { get; set; } = 1.0;
        public string? Description { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsFiltered { get; set; } = false;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Result of diagram export operation
    /// </summary>
    public class ExportResult
    {
        public bool Success { get; set; }
        public string Format { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string? ErrorMessage { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Types of diagrams supported
    /// </summary>
    public enum DiagramType
    {
        Class,
        Sequence,
        Architecture,
        DataFlow,
        Component,
        State,
        Deployment
    }
}