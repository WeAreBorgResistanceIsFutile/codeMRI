# CodeMRI MCP Server

Model Context Protocol (MCP) server that provides AI assistants with powerful code intelligence capabilities by exposing the CodeMRI AST graph through standardized query tools.

## Features

- **Code Graph Indexing**: Builds and maintains an in-memory graph of your codebase
- **Smart Query Gating**: Ensures queries are only served when the index is complete and synchronized
- **Incremental Updates**: Efficiently updates the graph when files change
- **Multi-Language Support**: C# (via Roslyn) and JavaScript/Python/Go/Rust/Java (via tree-sitter)
- **Rich Query Tools**: Find references, call hierarchies, implementations, dependencies, and more
- **Streamable HTTP Transport**: Docker-compatible HTTP transport for containerized deployment

## Installation

### Prerequisites

- .NET 10.0 SDK or later
- Node.js (for non-C# language parsing)

### Build

```bash
cd /Users/levente/AI/codeMRI
dotnet build codeMRI.MCP
```

### Publish

```bash
dotnet publish codeMRI.MCP -c Release -o ./publish/mcp
```

## Usage

### Local Development

Run the MCP server directly:

```bash
dotnet run --project codeMRI.MCP -- --repository /path/to/your/repo
```

### Configure with Claude Desktop

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "codeMRI-ast": {
      "command": "/Users/levente/AI/codeMRI/publish/mcp/codeMRI.MCP",
      "args": [
        "--repository", "/Users/levente/AI/codeMRI"
      ],
      "env": {
        "CODEMRI_REPO_PATH": "/Users/levente/AI/codeMRI"
      }
    }
  }
}
```

## Streamable HTTP Transport

The MCP server supports **Streamable HTTP transport** (MCP 2025-03-26 specification), which is essential for Docker deployments and integration with AI assistants like Antigravity.

### Why Streamable HTTP?

- **Docker Compatibility**: stdio transport doesn't work in containerized environments
- **Network Accessibility**: Allows remote AI assistants to connect over HTTP
- **Session Management**: Supports multiple concurrent client connections
- **Standard Protocol**: Implements the official MCP Streamable HTTP specification

### How It Works

The transport uses a single `/mcp` endpoint with three HTTP methods:

- **POST /mcp**: Send JSON-RPC messages (requests/notifications)
- **GET /mcp**: Open Server-Sent Events (SSE) stream for server-to-client messages
- **DELETE /mcp**: Terminate a session

### Configuration

The server automatically uses Streamable HTTP when running in Docker:

```yaml
# docker-compose.yml
mcp-server:
  ports:
    - "8080:8080"  # HTTP endpoint
  environment:
    - TRANSPORT_MODE=http  # Use Streamable HTTP transport
```

### Endpoints

- **MCP Endpoint**: `http://localhost:8080/mcp`
- **Health Check**: `http://localhost:8080/health` (coming soon)

### Session Management

The transport supports both:

1. **Session-based**: Client sends `Mcp-Session-Id` header after initialization
2. **Standalone SSE**: Client opens SSE stream without prior session

## Repository Configuration

### Docker Setup (Recommended)

By default, **the MCP server analyzes the codeMRI repository itself** (the directory where `docker-compose.yml` is located).

#### How It Works

The repository is configured via Docker volume mount in `docker-compose.yml`:

```yaml
mcp-server:
  volumes:
    - .:/repository:ro  # Current directory mounted as /repository
  environment:
    - CODEMRI_REPO_PATH=/repository
```

**What this means:**

- `.` = Current directory (where docker-compose.yml lives)
- Mounted at `/repository` inside the container (read-only for safety)
- `CODEMRI_REPO_PATH` environment variable tells the server where to look

#### Analyzing a Different Repository

**Method 1: Modify docker-compose.yml**

Edit the mcp-server volume:

```yaml
volumes:
  - /absolute/path/to/your/repo:/repository:ro
```

Then restart:

```bash
docker compose down
docker compose up -d
```

**Method 2: Run Standalone Container**

```bash
docker run -it --rm \
  -v "/path/to/your/repo:/repository:ro" \
  -e CODEMRI_REPO_PATH=/repository \
  -e AST_SERVICE_URL=http://host.docker.internal:3000 \
  --network code-mri-network \
  codemri-mcp-server:latest
```

**Method 3: Multiple Repositories**

Run multiple MCP server containers, each analyzing a different repo:

```bash
# Server 1 - codeMRI itself (via docker-compose)
docker compose up -d

# Server 2 - Another repo
docker run -d --name mcp-server-project2 \
  -v "/path/to/project2:/repository:ro" \
  -e CODEMRI_REPO_PATH=/repository \
  --network code-mri-network \
  codemri-mcp-server:latest
```

### Antigravity Integration

Antigravity is a powerful AI assistant that supports MCP via Streamable HTTP transport.

#### Configuration

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

#### Docker Setup for Antigravity

When using Docker, ensure the MCP server is accessible:

```bash
# Start all services including MCP server
docker compose up -d

# Verify MCP server is running
docker compose ps mcp-server

# Check logs
docker compose logs -f mcp-server
```

The MCP server will be available at `http://localhost:8080/mcp`.

#### Testing the Connection

Once configured, test the connection in Antigravity:

1. Open Antigravity
2. The codeMRI MCP server should appear in available servers
3. Try a query: "Find all references to `GraphIndexService`"
4. You should see results from your codebase

#### Troubleshooting

**Connection Refused**:

- Ensure Docker containers are running: `docker compose ps`
- Check MCP server logs: `docker compose logs mcp-server`
- Verify port 8080 is not in use: `lsof -i :8080`

**No Results Returned**:

- Wait for initial indexing to complete (check logs)
- Try refreshing the graph: "Refresh the code graph"
- Verify repository path is correct in docker-compose.yml

**Session Errors**:

- Restart the MCP server: `docker compose restart mcp-server`
- Clear Antigravity's MCP cache
- Check for network connectivity issues

## Configuration

### Command-Line Arguments

- `--repository <path>`: Path to the codebase to index (Priority 1)
- `--index-on-start <bool>`: Whether to build index at startup (default: true)
- `--update-strategy <event-driven|polling|manual|hybrid>`: How to detect code changes (default: event-driven)
- `--polling-interval-seconds <number>`: Polling interval if using polling strategy (default: 10)
- `--ast-service-url <url>`: URL for the ASTService (default: <http://localhost:3000>)
- `--log-level <level>`: Logging level (default: Information)

### Environment Variables

- `CODEMRI_REPO_PATH`: Path to the repository (Priority 2)

### Fallback

If neither command-line argument nor environment variable is set, the server will index the current working directory.

## Available Tools

All 7 tools are fully implemented and available:

### find_references

Find all references to a symbol (class, method, variable) in the codebase.

**Parameters:**

- `symbol` (required): The symbol name to search for
- `filePath` (optional): Optional file path to filter results

**Example:**

```
AI: Find all references to "OllamaLLMService"
```

### More Tools Coming Soon

- `call_hierarchy`: Get the call hierarchy (callers/callees)
- `find_implementations`: Find all implementations of an interface
- `query_dependencies`: Query the dependency graph
- `type_hierarchy`: Get the inheritance hierarchy
- `semantic_search`: Search for code elements by description
- `refresh_graph`: Manually trigger graph refresh

## Architecture

### Core Components

- **IndexStateService**: Tracks indexing status and gates queries to prevent stale results
- **GraphIndexService**: Builds and maintains the in-memory code graph
- **QueryEngine**: Executes graph queries (references, hierarchies, dependencies)
- **MCP Tools**: Expose query capabilities via Model Context Protocol

### Index States

- `Initializing`: Server starting up, no queries allowed
- `Indexing`: Building or updating index, queries blocked
- `Ready`: Index complete and synchronized, queries allowed
- `Stale`: Files changed but not yet indexed, queries blocked with warning

### Query Gating

All queries are gated through `IndexStateService.CheckQueryGateAsync()`. If the index is not ready, queries return:

```json
{
  "blocked": true,
  "reason": "Indexing in progress (45%). Please wait...",
  "retryAfterSeconds": 12
}
```

## Development

### Project Structure

```
codeMRI.MCP/
├── Program.cs                    # Entry point, host setup
├── Services/
│   ├── IndexStateService.cs     # Index state tracking
│   ├── GraphIndexService.cs     # Graph building/maintenance
│   └── QueryEngine.cs           # Graph query execution
├── Tools/
│   └── FindReferencesTool.cs    # MCP tool implementations
└── Models/
    └── IndexState.cs            # Data models
```

### Adding New Tools

1. Create a new class in `Tools/` directory
2. Inject `QueryEngine` and `IndexStateService`
3. Add `[Description]` attributes to methods
4. Implement query gating check
5. Return formatted results

Example:

```csharp
public class MyNewTool
{
    private readonly QueryEngine _queryEngine;
    private readonly IndexStateService _indexState;

    public MyNewTool(QueryEngine queryEngine, IndexStateService indexState)
    {
        _queryEngine = queryEngine;
        _indexState = indexState;
    }

    [Description("Description of what this tool does")]
    public async Task<string> MyToolAsync(
        [Description("Parameter description")] string param)
    {
        var gateResult = await _indexState.CheckQueryGateAsync();
        if (!gateResult.Allowed)
        {
            return $"❌ Query blocked: {gateResult.BlockReason}";
        }

        // Execute query
        var results = await _queryEngine.SomeQuery(param);
        return FormatResults(results);
    }
}
```

## Troubleshooting

### Index Not Building

- Check that the repository path is correct
- Ensure you have read permissions for all files
- Check logs for parsing errors

### Queries Always Blocked

- Wait for initial indexing to complete
- Check index state with logging
- Verify no files are pending indexing

### Performance Issues

- Reduce repository size by excluding unnecessary directories
- Increase polling interval if using polling strategy
- Consider using event-driven updates instead of polling

## License

Part of the CodeMRI project.
