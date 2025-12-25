using System.Text.Json.Serialization;

namespace codeMRI.MCP.Protocol;

// Initialize messages
public class InitializeParams
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "2024-11-05";
    
    [JsonPropertyName("capabilities")]
    public ClientCapabilities Capabilities { get; set; } = new();
    
    [JsonPropertyName("clientInfo")]
    public Implementation ClientInfo { get; set; } = new();
}

public class InitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "2024-11-05";
    
    [JsonPropertyName("capabilities")]
    public ServerCapabilities Capabilities { get; set; } = new();
    
    [JsonPropertyName("serverInfo")]
    public Implementation ServerInfo { get; set; } = new();
}

public class Implementation
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
}

public class ClientCapabilities
{
    [JsonPropertyName("roots")]
    public object? Roots { get; set; }
    
    [JsonPropertyName("sampling")]
    public object? Sampling { get; set; }
}

public class ServerCapabilities
{
    [JsonPropertyName("tools")]
    public ToolsCapability? Tools { get; set; }
    
    [JsonPropertyName("logging")]
    public object? Logging { get; set; }
}

public class ToolsCapability
{
    // Empty object to indicate tools are supported
}

// Tool messages
public class ListToolsResult
{
    [JsonPropertyName("tools")]
    public List<Tool> Tools { get; set; } = new();
}

public class Tool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("inputSchema")]
    public ToolInputSchema InputSchema { get; set; } = new();
}

public class ToolInputSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";
    
    [JsonPropertyName("properties")]
    public Dictionary<string, PropertySchema> Properties { get; set; } = new();
    
    [JsonPropertyName("required")]
    public List<string>? Required { get; set; }
}

public class PropertySchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class CallToolParams
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("arguments")]
    public Dictionary<string, object?>? Arguments { get; set; }
}

public class CallToolResult
{
    [JsonPropertyName("content")]
    public List<Content> Content { get; set; } = new();
    
    [JsonPropertyName("isError")]
    public bool? IsError { get; set; }
}

public class Content
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";
    
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}
