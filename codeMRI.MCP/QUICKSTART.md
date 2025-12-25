# CodeMRI MCP Server - Quick Start Guide

## What is this?

The CodeMRI MCP Server exposes your code's AST graph to AI assistants (like Claude) through the Model Context Protocol. This allows AI to intelligently query your codebase structure, find references, understand dependencies, and navigate call hierarchies.

## Quick Start (5 minutes)

### 1. Build the MCP Server

```bash
cd /Users/levente/AI/codeMRI
dotnet build codeMRI.MCP
```

### 2. Test Locally

```bash
dotnet run --project codeMRI.MCP -- --repository /Users/levente/AI/codeMRI
```

You should see:

```
CodeMRI MCP Server
Repository: /Users/levente/AI/codeMRI
Update Strategy: event-driven
Index on Start: true
Starting initial repository indexing...
```

Wait for indexing to complete (usually 10-30 seconds for medium repos).

### 3. Configure Your AI Assistant

#### Option A: Antigravity (Recommended for Docker)

Add to your `antigravity_config.json`:

```json
{
  "mcpServers": {
    "codeMRI": {
      "url": "http://localhost:8080/mcp",
      "transport": "streamable-http"
    }
  }
}
```

Then start with Docker:

```bash
# Start all services including MCP server
docker compose up -d

# Verify MCP server is running
docker compose ps mcp-server
```

#### Option B: Claude Desktop (Local Development)

Edit your Claude Desktop config file:

- **Mac**: `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`

Add this configuration:

```json
{
  "mcpServers": {
    "codeMRI": {
      "command": "/Users/levente/AI/codeMRI/codeMRI.MCP/bin/Debug/net10.0/codeMRI.MCP",
      "args": [
        "--repository", "/Users/levente/AI/codeMRI"
      ]
    }
  }
}
```

### 4. Restart Claude Desktop

Close and reopen Claude Desktop. You should see "codeMRI" in the available MCP servers.

### 5. Try It Out

Ask Claude:

> "Find all references to IASTServiceClient"

> "Show me the call hierarchy for ParseCodeAsync"

> "What classes implement ILLMClient?"

> "Show me the dependency tree for GraphIndexService"

## Available Tools

### 1. **find_references**

Find all places where a symbol is used.

**Example**: "Find all references to OllamaLLMService"

### 2. **call_hierarchy**

See who calls a method and what it calls.

**Example**: "Show me the call hierarchy for ChatAsync"

### 3. **find_implementations**

Find all implementations of an interface or base class.

**Example**: "What implements IASTServiceClient?"

### 4. **query_dependencies**

Explore component dependencies.

**Example**: "Show dependencies for GraphIndexService"

### 5. **type_hierarchy**

View inheritance relationships.

**Example**: "Show type hierarchy for BaseAgent"

### 6. **semantic_search**

Search for code elements by name or description.

**Example**: "Search for 'message composition'"

### 7. **refresh_graph**

Manually update the code graph.

**Example**: "Refresh the graph for all files"

## Troubleshooting

### "Query blocked: Indexing in progress"

Wait a few seconds for initial indexing to complete. The tool will tell you the estimated wait time.

### "No results found"

- Make sure the symbol name is exact (case-sensitive)
- Try semantic search instead
- Refresh the graph if you just added new code

### Server won't start

- Check that the repository path exists
- Ensure you have read permissions
- Look at console output for error messages

### Claude doesn't see the server

- Verify the config file path is correct
- Check that the executable path is absolute
- Restart Claude Desktop completely

## Performance Tips

- **First run**: Initial indexing takes 10-30 seconds for medium repos
- **Queries**: Most queries return in < 100ms once indexed
- **Updates**: File changes are detected automatically (if using event-driven strategy)

## What's Next?

- **Event-driven updates**: Integrate with codeMRI.Server for real-time updates
- **Docker deployment**: Run in a container for production use
- **Custom queries**: Add your own MCP tools in the `Tools/` directory

## Need Help?

Check the full README at `codeMRI.MCP/README.md` for detailed documentation.
