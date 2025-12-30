using codeMRI.Core.Interfaces;

namespace codeMRI.Core.Services;

public class MarkdownRepairService : IMarkdownRepairService
{
    public string ExtractMarkdown(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return string.Empty;

        var cleaned = response.Trim();

        // Strip any LLM preamble text before the actual markdown content
        cleaned = StripLLMPreamble(cleaned);

        // Remove markdown code fences if the LLM wrapped the entire output
        if (cleaned.StartsWith("```markdown")) 
            cleaned = cleaned.Substring("```markdown".Length).TrimStart('\n', '\r');
        else if (cleaned.StartsWith("```")) 
            cleaned = cleaned.Substring(3).TrimStart('\n', '\r');
            
        if (cleaned.EndsWith("```")) 
            cleaned = cleaned.Substring(0, cleaned.Length - 3).TrimEnd();

        return cleaned.Trim();
    }

    public string RepairMarkdown(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return markdown;

        // Convert [[WikiLink]] syntax to proper markdown links
        var repaired = ConvertWikiLinksToMarkdown(markdown);

        // Sanitize Mermaid diagrams
        repaired = SanitizeMermaidDiagrams(repaired);

        return repaired;
    }

    private string SanitizeMermaidDiagrams(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        var lines = content.Split('\n');
        var result = new System.Text.StringBuilder();
        var isInMermaidBlock = false;

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

            if (isInMermaidBlock && !string.IsNullOrWhiteSpace(line))
            {
                // 1. Handle labels in arrows first: A -->|Label| B
                // This ensures arrow labels are quoted and doesn't interfere with node labels.
                var arrowLabelPattern = @"([-=]>)\s*(\|)([^"" |][^|]*?)(\|)";
                var updatedLine = System.Text.RegularExpressions.Regex.Replace(line, arrowLabelPattern, match =>
                {
                    var arrow = match.Groups[1].Value;
                    var pipe1 = match.Groups[2].Value;
                    var label = match.Groups[3].Value.Trim();
                    var pipe2 = match.Groups[4].Value;

                    var escapedLabel = label.Replace("\"", "\\\"");
                    return $"{arrow}{pipe1}\"{escapedLabel}\"{pipe2}";
                });

                // 2. Find and quote node labels: ID[Label], ID(Label), ((Label)), etc.
                // We use specific patterns for each node shape to handle nesting better.
                // The lookahead ensures we match until the next arrow or EOL.
                var arrowLookahead = @"(?=\s*(?:---?|==>|--|-.->|->|=>|\|)|$)";
                var shapes = new[]
                {
                    (@"\[\[", @"\]\]"), // Subroutine
                    (@"\(\(", @"\)\)"), // Circle
                    (@"\(\[", @"\]\)"), // Stadium
                    (@"\[\(", @"\)]"), // Cylinder
                    (@"\[/", @"/\]"),   // Parallelogram / Trapezoid
                    (@"\[\\", @"\\\]"), // Parallelogram / Trapezoid
                    (@"\{\{", @"\}\}"), // Hexagon
                    (@"\[", @"\]"),     // Rectangle
                    (@"\(", @"\)"),     // Rounded
                    (@"\{", @"\}")      // Rhombus
                };

                foreach (var shape in shapes)
                {
                    var open = shape.Item1;
                    var close = shape.Item2;
                    var pattern = $@"(?<![-=]){open}([^"" ].*?){close}{arrowLookahead}";
                    updatedLine = System.Text.RegularExpressions.Regex.Replace(updatedLine, pattern, match =>
                    {
                        var label = match.Groups[1].Value.Trim();
                        var escapedLabel = label.Replace("\"", "\\\"");
                        return $"{open.Replace("\\", "")}\"{escapedLabel}\"{close.Replace("\\", "")}";
                    });
                }

                result.AppendLine(updatedLine);
            }
            else
            {
                result.AppendLine(line);
            }
        }

        return result.ToString().TrimEnd();
    }

    private string StripLLMPreamble(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        var lines = content.Split('\n');
        var startIndex = -1;

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmedLine = lines[i].TrimStart();
            
            // Found the start of actual markdown content
            if (trimmedLine.StartsWith("# ") || trimmedLine.StartsWith("```markdown") || trimmedLine.StartsWith("```"))
            {
                startIndex = i;
                break;
            }
        }

        // If we found a markdown start, remove everything before it
        if (startIndex > 0)
        {
            return string.Join('\n', lines.Skip(startIndex));
        }

        // No clear markdown start found, return as-is
        return content;
    }

    private string ConvertWikiLinksToMarkdown(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        // Pattern to match [[WikiLink]] or [[Display Text|PageName]]
        var wikiLinkPattern = @"\[\[([^\]|]+)(?:\|([^\]]+))?\]\]";
        
        return System.Text.RegularExpressions.Regex.Replace(content, wikiLinkPattern, match =>
        {
            var linkTarget = match.Groups[1].Value.Trim();
            var displayText = match.Groups[2].Success ? match.Groups[2].Value.Trim() : linkTarget;
            
            // Convert to markdown link with anchor
            // For wiki-style links, we'll use lowercase-dash format for anchors
            var anchor = linkTarget.ToLower().Replace(' ', '-').Replace(".", "");
            return $"[{displayText}](#{anchor})";
        });
    }
}
