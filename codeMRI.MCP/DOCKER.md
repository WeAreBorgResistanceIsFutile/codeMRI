# codeMRI MCP Server - Docker Usage

## Building the Image

```bash
# Build the MCP server image
docker build -t codemri-mcp-server:latest -f codeMRI.MCP/Dockerfile .
```

## Running with Docker Compose

The MCP server is included in the main docker-compose.yml:

```bash
# Rebuild and start all services
./rebuild-and-start.sh

# Or manually
docker compose up -d
```

## Running Standalone

```bash
# Run MCP server with current directory as repository
docker run -it --rm \
  -v "$(pwd):/repository:ro" \
  -e CODEMRI_REPO_PATH=/repository \
  -e AST_SERVICE_URL=http://host.docker.internal:3000 \
  --network code-mri-network \
  codemri-mcp-server:latest
```

## Environment Variables

- `CODEMRI_REPO_PATH`: Path to code repository (default: `/repository`)
- `AST_SERVICE_URL`: AST service endpoint (default: `http://ast-service:3002`)
- `UPDATE_STRATEGY`: Change detection strategy: `hybrid` or `polling` (default: `hybrid`)
- `INDEX_ON_START`: Initial indexing on startup (default: `true`)

## Connecting from Claude Desktop

Update your Claude Desktop MCP config to use the Docker container:

```json
{
  "mcpServers": {
    "codeMRI": {
      "command": "docker",
      "args": [
        "exec",
        "-i",
        "code-mri-mcp-server",
        "dotnet",
        "/app/codeMRI.MCP.dll",
        "--repository",
        "/repository"
      ]
    }
  }
}
```

## View Logs

```bash
# All MCP server logs
docker compose logs -f mcp-server

# Last 100 lines
docker compose logs --tail=100 mcp-server
```

## Troubleshooting

### Container exits immediately

- Check logs: `docker compose logs mcp-server`
- Ensure AST service is running: `docker compose ps ast-service`

### Repository not found

- Verify volume mount in docker-compose.yml
- Check `CODEMRI_REPO_PATH` environment variable

### Cannot connect to Neo4j/Qdrant

- Ensure all services are on same network: `code-mri-network`
- Check service health: `docker compose ps`
