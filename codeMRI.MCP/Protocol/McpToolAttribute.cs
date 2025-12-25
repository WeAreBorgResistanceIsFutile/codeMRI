namespace codeMRI.MCP.Protocol;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class McpToolAttribute : Attribute
{
    public string? Name { get; set; }
    
    public McpToolAttribute(string? name = null)
    {
        Name = name;
    }
}
