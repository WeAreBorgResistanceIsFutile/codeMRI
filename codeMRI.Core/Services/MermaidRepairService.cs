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

    public string RepairMermaid(string mermaid)
    {
        if (string.IsNullOrWhiteSpace(mermaid))
            return mermaid;

        var lines = mermaid.Split('\n');
        var result = new System.Text.StringBuilder();
        var hasMermaidBlocks = mermaid.Contains("```mermaid");
        var isInMermaidBlock = !hasMermaidBlocks;

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

            // Pre-process: fix common arrow label hallucinations like -->"|Label|
            updatedLine = updatedLine.Replace("-->\"|", "-->|");
            updatedLine = updatedLine.Replace("-->\" |", "-->|");
            
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

                    if (label.StartsWith("\"")) return m.Value;

                    return $"{id}{opener}\"{label.Replace("\"", "\\\"")}\"{closer}";
                });
            }

            result.AppendLine(updatedLine);
        }

        return result.ToString().TrimEnd();
    }
}
