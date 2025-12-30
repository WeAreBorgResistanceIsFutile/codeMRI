using System.Text.Json;
using System.Text.Json.Serialization;

namespace codeMRI.Core.Services;

internal class JudgeResponse
{
    [JsonPropertyName("requirement_id")]
    public string RequirementId { get; set; } = string.Empty;

    public double Score { get; set; }
    public string? Reasoning { get; set; }
    public List<EvidenceItem>? Evidence { get; set; }
}

[JsonConverter(typeof(EvidenceItemConverter))]
internal class EvidenceItem
{
    [JsonPropertyName("doc_section")]
    public string DocSection { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    public override string ToString() => string.IsNullOrWhiteSpace(DocSection) ? Details : $"{DocSection}: {Details}";
}

internal class EvidenceItemConverter : JsonConverter<EvidenceItem>
{
    public override EvidenceItem? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new EvidenceItem { Details = reader.GetString() ?? string.Empty };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            
            var item = new EvidenceItem();
            
            // Handle multiple possible property names for robustness
            if (root.TryGetProperty("doc_section", out var sectionProp))
                item.DocSection = sectionProp.GetString() ?? string.Empty;
            else if (root.TryGetProperty("docSection", out sectionProp))
                item.DocSection = sectionProp.GetString() ?? string.Empty;
            else if (root.TryGetProperty("section", out sectionProp))
                item.DocSection = sectionProp.GetString() ?? string.Empty;
            else if (root.TryGetProperty("file", out sectionProp))
                item.DocSection = sectionProp.GetString() ?? string.Empty;

            if (root.TryGetProperty("details", out var detailsProp))
                item.Details = detailsProp.GetString() ?? string.Empty;
            else if (root.TryGetProperty("line", out detailsProp))
                item.Details = $"Line {detailsProp.GetRawText()}";
            else if (root.TryGetProperty("reasoning", out detailsProp)) // Sometimes LLMs put reasoning in evidence items
                item.Details = detailsProp.GetString() ?? string.Empty;
            
            return item;
        }

        throw new JsonException($"Unexpected token type {reader.TokenType} for EvidenceItem");
    }

    public override void Write(Utf8JsonWriter writer, EvidenceItem value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("doc_section", value.DocSection);
        writer.WriteString("details", value.Details);
        writer.WriteEndObject();
    }
}