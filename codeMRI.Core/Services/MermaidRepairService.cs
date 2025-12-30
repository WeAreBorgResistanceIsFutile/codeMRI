using codeMRI.Core.Interfaces;

namespace codeMRI.Core.Services;

public class MermaidRepairService : IMermaidRepairService
{
    public string? ExtractMermaid(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        const string startMarker = "```mermaid";
        const string endMarker = "```";

        var startIndex = response.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
            return null;

        startIndex += startMarker.Length;
        var endIndex = response.IndexOf(endMarker, startIndex, StringComparison.OrdinalIgnoreCase);

        if (endIndex < 0)
        {
            // If no closing marker, just take everything from start marker
            return response.Substring(startIndex).Trim();
        }

        return response.Substring(startIndex, endIndex - startIndex).Trim();
    }

    /// <summary>
    ///     Checks if content looks like raw mermaid syntax (without code fences).
    ///     Detects common mermaid diagram types.
    /// </summary>
    private bool IsRawMermaidSyntax(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var trimmed = content.TrimStart();
        
        // Check for common mermaid diagram type keywords
        return trimmed.StartsWith("graph ") ||
               trimmed.StartsWith("flowchart ") ||
               trimmed.StartsWith("sequenceDiagram") ||
               trimmed.StartsWith("classDiagram") ||
               trimmed.StartsWith("stateDiagram") ||
               trimmed.StartsWith("erDiagram") ||
               trimmed.StartsWith("gantt") ||
               trimmed.StartsWith("pie") ||
               trimmed.StartsWith("journey") ||
               trimmed.StartsWith("gitGraph") ||
               trimmed.StartsWith("C4Context") ||
               trimmed.StartsWith("mindmap") ||
               trimmed.StartsWith("timeline");
    }

    public string RepairMermaid(string mermaid)
    {
        if (string.IsNullOrWhiteSpace(mermaid))
            return mermaid;

        // Check if this content contains mermaid blocks or looks like raw mermaid syntax
        var hasMermaidBlocks = mermaid.Contains("```mermaid");
        var looksLikeMermaid = IsRawMermaidSyntax(mermaid);
        
        // If it's neither fenced mermaid nor raw mermaid syntax, return unchanged
        // This prevents processing regular markdown content
        if (!hasMermaidBlocks && !looksLikeMermaid)
            return mermaid;

        var lines = mermaid.Split('\n');
        var result = new System.Text.StringBuilder();
        var isInMermaidBlock = looksLikeMermaid;  // If raw mermaid, process all lines; otherwise wait for fence
        
        var clickInteractions = new System.Collections.Generic.List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("```mermaid"))
            {
                isInMermaidBlock = true;
                result.AppendLine(line);
                continue;
            }
            if (isInMermaidBlock && trimmedLine.StartsWith("```"))
            {
                isInMermaidBlock = false;
                result.AppendLine(line);
                continue;
            }

            if (!isInMermaidBlock)
            {
                result.AppendLine(line);
                continue;
            }

            var updatedLine = line;

            // 0. Subgraph sanitization: subgraph Name With Spaces -> subgraph Name_With_Spaces ["Name With Spaces"]
            // Match subgraph followed by space and then text (until end or comment)
            if (trimmedLine.StartsWith("subgraph "))
            {
                var subgraphMatch = System.Text.RegularExpressions.Regex.Match(updatedLine, @"subgraph\s+(?<name>[^""\[\]\n]+?)\s*$");
                if (subgraphMatch.Success)
                {
                    var name = subgraphMatch.Groups["name"].Value.Trim();
                    if (name.Contains(" "))
                    {
                        var sanitizedId = name.Replace(" ", "_");
                        updatedLine = updatedLine.Replace(name, $"{sanitizedId} [\"{name}\"]");
                    }
                }
                result.AppendLine(updatedLine);
                continue;
            }

            // Pre-process: fix common arrow label hallucinations like -->"|Label|
            updatedLine = updatedLine.Replace("-->\"|", "-->|");
            updatedLine = updatedLine.Replace("-->\" |", "-->|");
            
            // 0.5. Fix broken link syntax: ID[Label](Url) -> ID["Label"], click ID Url
            // We use a regex to capture ID, Label (simple), and Url
            updatedLine = System.Text.RegularExpressions.Regex.Replace(updatedLine, @"(?<=^|\s|-->|---|==>|\|)(?<id>[A-Za-z0-9_-]+)\s*\[(?<label>[^\]]+)\]\((?<url>[^)]+)\)", m => 
            {
                var id = m.Groups["id"].Value;
                var label = m.Groups["label"].Value;
                var url = m.Groups["url"].Value;
                
                // Escape generics in label
                label = label.Replace("<", " &lt;").Replace(">", "&gt;");
                
                // Add click interaction
                clickInteractions.Add($"click {id} {url}");

                return $"{id}[\"{label}\"]";
            });

            // 1. Arrow labels: A -->|Label| B
            updatedLine = System.Text.RegularExpressions.Regex.Replace(updatedLine, @"(\|)([^""|]+?)(\|)", m => 
            {
                var label = m.Groups[2].Value.Trim();
                if (label.StartsWith("\"") && label.EndsWith("\"")) return m.Value;
                return $"|\"{label.Replace("\"", "\\\"")}\"|";
            });

            // 2. Node labels: ID[Label]
            // We use C# balanced groups to handle nesting correctly
            var shapes = new[]
            {
                (@"\[\[", @"\]\]", @"\[\[(?>\[\[(?<D>)|\]\](?<-D>)|.)*(?(D)(?!))\]\]"),
                (@"\(\(", @"\)\)", @"\(\((?>\(\((?<D>)|\)\)(?<-D>)|.)*(?(D)(?!))\)\)"),
                (@"\[", @"\]", @"\[(?>\[(?<D>)|\](?<-D>)|[^\[\]]*)*(?(D)(?!))\]"),
                (@"\(", @"\)", @"\((?>\((?<D>)|\)(?<-D>)|[^()]*)*(?(D)(?!))\)"),
                (@"\{", @"\}", @"\{(?>\{(?<D>)|\}(?<-D>)|[^{}]*)*(?(D)(?!))\}")
            };

            foreach (var shape in shapes)
            {
                var pattern = $@"(?<=^|\s|-->|---|==>|\|)([A-Za-z0-9_-]+)\s*({shape.Item3})(?=\s|--|==|$)";
                
                updatedLine = System.Text.RegularExpressions.Regex.Replace(updatedLine, pattern, m => 
                {
                    var id = m.Groups[1].Value;
                    var fullMatch = m.Groups[2].Value;
                    
                    // Extract opener and closer
                    var opener = shape.Item1.Replace("\\", "");
                    var closer = shape.Item2.Replace("\\", "");
                    
                    var label = fullMatch.Substring(opener.Length, fullMatch.Length - opener.Length - closer.Length).Trim();
                    
                    // Heuristic fixes for LLM hallucinations:
                    // 1. Fix broken function calls: (" -> ()
                    if (label.EndsWith("(\"")) label = label.Substring(0, label.Length - 2) + "()";
                    // 2. Fix broken function calls: ") -> )
                    if (label.EndsWith("\")")) label = label.Substring(0, label.Length - 2) + ")";

                    // 3. Fix unbalanced quotes (if odd number of quotes)
                    var quoteCount = label.Split('"').Length - 1;
                    if (quoteCount % 2 != 0)
                    {
                        // Prioritize stripping leading quote if present, otherwise trailing
                        if (label.StartsWith("\"")) label = label.Substring(1);
                        else if (label.EndsWith("\"")) label = label.Substring(0, label.Length - 1);
                    }
                    
                    // Escape generics
                    if (label.Contains("<") || label.Contains(">")) 
                    {
                        label = label.Replace("<", " &lt;").Replace(">", "&gt;");
                    }

                    if (label.StartsWith("\"")) 
                    {
                        // Even if already quoted, we might have escaped generics, so we construct the string again
                         return $"{id}{opener}{label}{closer}";
                    }

                    return $"{id}{opener}\"{label.Replace("\"", "\\\"")}\"{closer}";
                });
            }

            result.AppendLine(updatedLine);
        }
        
        if (clickInteractions.Count > 0)
        {
            result.AppendLine();
            result.AppendLine("%% Click interactions/Links");
            foreach (var click in clickInteractions)
            {
                result.AppendLine(click);
            }
        }

        return result.ToString().TrimEnd();
    }
}
