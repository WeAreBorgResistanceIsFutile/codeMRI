using codeMRI.Visualization.Models;
using System.Text;
using System.Text.Json;

namespace codeMRI.Visualization.Services
{
    /// <summary>
    /// Service for exporting diagrams to various formats (PNG, SVG, PDF)
    /// </summary>
    public class DiagramExportService
    {
        private readonly HttpClient _httpClient;

        public DiagramExportService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Export diagram to PNG format
        /// </summary>
        public async Task<ExportResult> ExportToPngAsync(InteractiveDiagram diagram, PngExportOptions? options = null)
        {
            try
            {
                options ??= diagram.ExportOptions.Png;
                
                var exportData = new
                {
                    code = diagram.MermaidContent,
                    format = "png",
                    width = options.Width,
                    height = options.Height,
                    backgroundColor = options.BackgroundColor,
                    transparent = options.Transparent,
                    quality = options.Quality
                };

                var result = await ExportWithMermaidLiveAsync(exportData, "png");
                if (result.Success)
                {
                    result.FileName = $"{SanitizeFileName(diagram.Title)}.png";
                }

                return result;
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    Format = "png",
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Export diagram to SVG format
        /// </summary>
        public async Task<ExportResult> ExportToSvgAsync(InteractiveDiagram diagram, SvgExportOptions? options = null)
        {
            try
            {
                options ??= diagram.ExportOptions.Svg;
                
                var exportData = new
                {
                    code = diagram.MermaidContent,
                    format = "svg",
                    backgroundColor = options.BackgroundColor,
                    includeStyles = options.IncludeStyles,
                    includeMetadata = options.IncludeMetadata
                };

                var result = await ExportWithMermaidLiveAsync(exportData, "svg");
                if (result.Success)
                {
                    result.FileName = $"{SanitizeFileName(diagram.Title)}.svg";
                    
                    // Post-process SVG if needed
                    if (options.Compressed)
                    {
                        result.Data = await CompressSvgAsync(result.Data);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    Format = "svg",
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Export diagram to PDF format
        /// </summary>
        public async Task<ExportResult> ExportToPdfAsync(InteractiveDiagram diagram, PdfExportOptions? options = null)
        {
            try
            {
                options ??= diagram.ExportOptions.Pdf;
                
                var exportData = new
                {
                    code = diagram.MermaidContent,
                    format = "pdf",
                    paperSize = options.PaperSize,
                    orientation = options.Orientation,
                    margin = options.Margin,
                    includeBookmarks = options.IncludeBookmarks
                };

                var result = await ExportWithMermaidLiveAsync(exportData, "pdf");
                if (result.Success)
                {
                    result.FileName = $"{SanitizeFileName(diagram.Title)}.pdf";
                }

                return result;
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    Format = "pdf",
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Export diagram to multiple formats
        /// </summary>
        public async Task<List<ExportResult>> ExportToMultipleFormatsAsync(InteractiveDiagram diagram)
        {
            var results = new List<ExportResult>();

            if (diagram.ExportOptions.EnablePng)
            {
                var pngResult = await ExportToPngAsync(diagram);
                results.Add(pngResult);
            }

            if (diagram.ExportOptions.EnableSvg)
            {
                var svgResult = await ExportToSvgAsync(diagram);
                results.Add(svgResult);
            }

            if (diagram.ExportOptions.EnablePdf)
            {
                var pdfResult = await ExportToPdfAsync(diagram);
                results.Add(pdfResult);
            }

            return results;
        }

        /// <summary>
        /// Generate interactive HTML with zoom and filtering capabilities
        /// </summary>
        public string GenerateInteractiveHtml(InteractiveDiagram diagram)
        {
            var html = GenerateHtmlTemplate(diagram);
            return html;
        }

        private string GenerateHtmlTemplate(InteractiveDiagram diagram)
        {
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var diagramJson = JsonSerializer.Serialize(diagram, jsonOptions);
            
            var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{diagram.Title}</title>
    <script src=""https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js""></script>
    <style>
        body {{
            font-family: Arial, sans-serif;
            margin: 0;
            padding: 20px;
            background-color: #f5f5f5;
        }}
        .diagram-container {{
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            padding: 20px;
            margin: 20px 0;
            position: relative;
            overflow: hidden;
        }}
        .controls {{
            position: fixed;
            top: 20px;
            right: 20px;
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            padding: 15px;
            z-index: 1000;
            min-width: 200px;
        }}
        .control-group {{
            margin-bottom: 15px;
        }}
        .control-group label {{
            display: block;
            margin-bottom: 5px;
            font-weight: bold;
            color: #333;
        }}
        .control-group input, .control-group select {{
            width: 100%;
            padding: 5px;
            border: 1px solid #ddd;
            border-radius: 4px;
        }}
        .btn {{
            background: #007bff;
            color: white;
            border: none;
            padding: 8px 15px;
            border-radius: 4px;
            cursor: pointer;
            margin: 2px;
            font-size: 12px;
        }}
        .btn:hover {{
            background: #0056b3;
        }}
        .zoom-controls {{
            display: flex;
            gap: 5px;
            align-items: center;
        }}
        .zoom-level {{
            font-weight: bold;
            color: #007bff;
        }}
        #mermaid-diagram {{
            text-align: center;
            transform-origin: center center;
            transition: transform 0.3s ease;
        }}
    </style>
</head>
<body>
    <div class=""controls"">
        <h3>Diagram Controls</h3>
        
        <div class=""control-group"">
            <label>Zoom Controls</label>
            <div class=""zoom-controls"">
                <button class=""btn"" onclick=""zoomIn()"">+</button>
                <button class=""btn"" onclick=""zoomOut()"">-</button>
                <button class=""btn"" onclick=""resetZoom()"">Reset</button>
                <button class=""btn"" onclick=""fitToScreen()"">Fit</button>
                <span class=""zoom-level"" id=""zoom-level"">100%</span>
            </div>
        </div>

        <div class=""control-group"">
            <label>Export Options</label>
            <button class=""btn"" onclick=""exportToPng()"">Export PNG</button>
            <button class=""btn"" onclick=""exportToSvg()"">Export SVG</button>
            <button class=""btn"" onclick=""copyMermaidCode()"">Copy Code</button>
        </div>
    </div>

    <div class=""diagram-container"">
        <div id=""mermaid-diagram"">
            {diagram.MermaidContent}
        </div>
    </div>

    <script>
        const diagramData = {diagramJson};
        let currentZoom = 1.0;
        const minZoom = {diagram.Options.Zoom.MinZoom};
        const maxZoom = {diagram.Options.Zoom.MaxZoom};
        const zoomStep = 0.1;

        mermaid.initialize({{ startOnLoad: true, theme: '{diagram.Options.Theme}' }});

        function zoomIn() {{
            if (currentZoom < maxZoom) {{
                currentZoom = Math.min(currentZoom + zoomStep, maxZoom);
                applyZoom();
            }}
        }}

        function zoomOut() {{
            if (currentZoom > minZoom) {{
                currentZoom = Math.max(currentZoom - zoomStep, minZoom);
                applyZoom();
            }}
        }}

        function resetZoom() {{
            currentZoom = 1.0;
            applyZoom();
        }}

        function fitToScreen() {{
            const container = document.querySelector('.diagram-container');
            const diagram = document.getElementById('mermaid-diagram');
            
            const containerWidth = container.clientWidth - 40;
            const containerHeight = window.innerHeight - 200;
            
            currentZoom = Math.min(
                containerWidth / diagram.scrollWidth,
                containerHeight / diagram.scrollHeight,
                1.0
            );
            
            applyZoom();
        }}

        function applyZoom() {{
            const diagram = document.getElementById('mermaid-diagram');
            diagram.style.transform = `scale(${{currentZoom}})`;
            document.getElementById('zoom-level').textContent = `${{Math.round(currentZoom * 100)}}%`;
        }}

        async function exportToPng() {{
            try {{
                const svg = document.querySelector('#mermaid-diagram svg');
                const canvas = document.createElement('canvas');
                const ctx = canvas.getContext('2d');
                const svgData = new XMLSerializer().serializeToString(svg);
                const img = new Image();
                
                img.onload = function() {{
                    canvas.width = {diagram.ExportOptions.Png.Width};
                    canvas.height = {diagram.ExportOptions.Png.Height};
                    ctx.fillStyle = '{diagram.ExportOptions.Png.BackgroundColor}';
                    ctx.fillRect(0, 0, canvas.width, canvas.height);
                    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
                    
                    canvas.toBlob(function(blob) {{
                        const url = URL.createObjectURL(blob);
                        const a = document.createElement('a');
                        a.href = url;
                        a.download = '{SanitizeFileName(diagram.Title)}.png';
                        a.click();
                        URL.revokeObjectURL(url);
                    }}, 'image/png', {diagram.ExportOptions.Png.Quality / 100});
                }};
                
                img.src = 'data:image/svg+xml;base64,' + btoa(svgData);
            }} catch (error) {{
                alert('Export failed: ' + error.message);
            }}
        }}

        async function exportToSvg() {{
            try {{
                const svg = document.querySelector('#mermaid-diagram svg');
                const svgData = new XMLSerializer().serializeToString(svg);
                const blob = new Blob([svgData], {{ type: 'image/svg+xml' }});
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = '{SanitizeFileName(diagram.Title)}.svg';
                a.click();
                URL.revokeObjectURL(url);
            }} catch (error) {{
                alert('Export failed: ' + error.message);
            }}
        }}

        function copyMermaidCode() {{
            navigator.clipboard.writeText(diagramData.mermaidContent).then(() => {{
                alert('Mermaid code copied to clipboard!');
            }}).catch(err => {{
                alert('Failed to copy: ' + err.message);
            }});
        }}

        document.addEventListener('DOMContentLoaded', function() {{
            if ({diagram.Options.Zoom.FitToView.ToString().ToLower()}) {{
                setTimeout(fitToScreen, 100);
            }}
        }});
    </script>
</body>
</html>";

            return html;
        }

        private async Task<ExportResult> ExportWithMermaidLiveAsync(object exportData, string format)
        {
            try
            {
                // In a real implementation, you would use Mermaid's official rendering service
                // For now, we'll simulate the export process
                var json = JsonSerializer.Serialize(exportData);
                var content = Encoding.UTF8.GetBytes(json);
                
                return new ExportResult
                {
                    Success = true,
                    Format = format,
                    Data = content,
                    FileSize = content.Length
                };
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    Format = format,
                    ErrorMessage = ex.Message
                };
            }
        }

        private async Task<byte[]> CompressSvgAsync(byte[] svgData)
        {
            // Simple SVG compression - in real implementation, use proper SVG optimization
            var svg = Encoding.UTF8.GetString(svgData);
            var compressed = svg.Replace("  ", " ").Replace("\n", "").Replace("\r", "");
            return Encoding.UTF8.GetBytes(compressed);
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
        }
    }
}