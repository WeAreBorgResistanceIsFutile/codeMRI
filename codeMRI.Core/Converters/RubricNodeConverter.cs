using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using codeMRI.Core.Interfaces;

namespace codeMRI.Core.Converters;

public class RubricNodeConverter : JsonConverter<RubricNode>
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(RubricNode).IsAssignableFrom(typeToConvert);
    }

    public override RubricNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;

        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            var root = doc.RootElement;
            return ReadRubricNode(root, typeToConvert, options);
        }
    }

    private RubricNode ReadRubricNode(JsonElement element, Type typeToConvert, JsonSerializerOptions options)
    {
        bool isLeaf = false;
        if (TryGetPropertyCaseInsensitive(element, "is_leaf", out var isLeafProp) && 
            (isLeafProp.ValueKind == JsonValueKind.True))
        {
            isLeaf = true;
        }
        else if (TryGetPropertyCaseInsensitive(element, "isLeaf", out var isLeafPropCam) &&
                 (isLeafPropCam.ValueKind == JsonValueKind.True))
        {
            isLeaf = true;
        }

        RubricNode node;

        if (typeToConvert == typeof(EvaluationRubric))
        {
            node = new EvaluationRubric();
        }
        else if (isLeaf)
        {
            var req = new RubricRequirement();
            if (TryGetPropertyCaseInsensitive(element, "description", out var desc))
                req.Description = desc.GetString() ?? string.Empty;
            node = req;
        }
        else
        {
            node = new RubricCategory();
        }

        // Common properties
        if (TryGetPropertyCaseInsensitive(element, "title", out var title))
            node.Title = title.GetString() ?? string.Empty;

        if (TryGetPropertyCaseInsensitive(element, "weight", out var weight) && weight.ValueKind == JsonValueKind.Number)
            node.Weight = weight.GetDouble();

        node.IsLeaf = isLeaf;

        if (TryGetPropertyCaseInsensitive(element, "children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            node.Children = new List<RubricNode>();
            foreach (var child in children.EnumerateArray())
            {
                // Recursive call
                // We pass RubricNode as type so it re-enters this converter logic for decision making
                var childNode = ReadRubricNode(child, typeof(RubricNode), options);
                node.Children.Add(childNode);
            }
        }

        return node;
    }

    public override void Write(Utf8JsonWriter writer, RubricNode value, JsonSerializerOptions options)
    {
        // Simple serialization - usually not the issue here
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }

    private bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value)) return true;
        
        // Simple manual scan for case-insensitive match if direct lookup fails
        foreach (var prop in element.EnumerateObject())
        {
            // Normalize property names (handle snake_case vs camelCase)
            string normalizedProp = prop.Name.Replace("_", "").ToLowerInvariant();
            string normalizedTarget = propertyName.Replace("_", "").ToLowerInvariant();

            if (normalizedProp == normalizedTarget)
            {
                value = prop.Value;
                return true;
            }
        }
        
        value = default;
        return false;
    }
}
