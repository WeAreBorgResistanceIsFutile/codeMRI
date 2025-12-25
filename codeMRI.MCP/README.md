# CodeMRI MCP Server

Model Context Protocol (MCP) server that provides AI assistants with powerful code intelligence capabilities by exposing the CodeMRI AST graph through standardized query tools.

## Features

- **Code Graph Indexing**: Builds and maintains an in-memory graph of your codebase
- **Smart Query Gating**: Ensures queries are only served when the index is complete and synchronized
- **Incremental Updates**: Efficiently updates the graph when files change
- **Multi-Language Support**: C# (via Roslyn) and JavaScript/Python/Go/Rust/Java (via tree-sitter)
- **Rich Query Tools**: Find references, call hierarchies, implementations, dependencies, and more

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
