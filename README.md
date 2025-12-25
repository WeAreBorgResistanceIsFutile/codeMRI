# codeMRI

**codeMRI** is an advanced AI-powered code analysis and documentation platform that generates comprehensive, multi-perspective repository documentation using LLM-based agents and semantic analysis. Based on the CodeWiki research, it provides scalable, context-aware documentation generation with intelligent model routing and adaptive processing.

## 🚀 Features

### Core Capabilities

- **Intelligent Repository Ingestion**: Supports both local and remote Git repositories with automatic language detection
- **Multi-Language AST Parsing**: Deep code analysis via dedicated AST service supporting TypeScript/JavaScript, Python, C#, Java, Go, Rust, and more
- **Hierarchical Decomposition**: Automatically structures repositories into logical modules and components using semantic clustering
- **Dependency Graph Analysis**: Visualizes code relationships and dependencies in Neo4j graph database
- **Semantic Search**: RAG-based chat interface for querying codebases with vector similarity search
- **Agent Telemetry**: Comprehensive tracking and monitoring of AI agent activities throughout the generation process
- **Re-ingestion Support**: Intelligent updates for evolving repositories

### Advanced AI Features

- **Audience-Specific Documentation**: Generate tailored documentation for different audiences (Developer, Tester, DevOps)
- **Model Routing**: Intelligent task-specific model selection for optimal results
  - Code analysis using specialized models (e.g., `glm-4.6:cloud`)
  - Natural language generation with optimized models (e.g., `ministral-3:14b-cloud`)
  - Ensemble synthesis for high-quality output
- **Multi-Perspective Evaluation**: Documentation judged and synthesized from multiple LLM perspectives using consensus-based quality control
- **Adaptive Delegation**: Dynamic complexity-based delegation with configurable depth and semantic diversity thresholds
- **Hierarchical Context Management**: Map-Reduce style processing for large codebases with entity anchoring and parent-child revision loops
- **Intelligent Message Composition**: Multiple strategies for handling content of varying sizes
  - **Direct Strategy**: For content that fits within context window
  - **Chunking Strategy**: Iterative processing of large content with LLM calls, prompt reformulation, and result synthesis
  - **Context Window Management**: Automatic validation to prevent exceeding LLM context limits
- **Interactive Citations**: RAG responses include styled citation markers with hover popovers showing source document details

## 🤖 MCP Server

codeMRI includes a **Model Context Protocol (MCP) server** that exposes the codebase's AST graph to AI assistants like Antigravity and Claude Desktop, enabling intelligent code queries and navigation.

### MCP Features

- **7 Powerful Query Tools**:
  - `find_references` - Find all references to a symbol (class, method, variable)
  - `call_hierarchy` - Explore who calls a method and what it calls
  - `find_implementations` - Discover all implementations of an interface or base class
  - `query_dependencies` - Navigate component dependencies and dependents
  - `type_hierarchy` - View inheritance relationships (ancestors/descendants)
  - `semantic_search` - Search for code elements by name or description
  - `refresh_graph` - Manually update the code graph

- **4 Update Strategies**:
  - **Event-driven**: Real-time updates via HTTP API integration
  - **Polling**: Periodic file system scanning for changes
  - **Hybrid**: FileSystemWatcher with automatic polling fallback
  - **Manual**: Explicit refresh control via AI commands

- **Streamable HTTP Transport**: Docker-compatible transport for containerized deployment
- **Multi-Language Support**: C# (Roslyn) + JavaScript/Python/Go/Rust/Java (tree-sitter)
- **Smart Query Gating**: Ensures queries only execute when index is ready and synchronized
- **Incremental Updates**: Efficiently updates graph when files change

### Quick Start with MCP

#### Docker Deployment (Recommended)

The MCP server is included in `docker-compose.yml`:

```bash
docker compose up -d
```

The server will be available at `http://localhost:8080` with Streamable HTTP transport.

#### Antigravity Configuration

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

#### Claude Desktop Configuration

For local development, add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "codeMRI": {
      "command": "/path/to/codeMRI.MCP/bin/Debug/net10.0/codeMRI.MCP",
      "args": ["--repository", "/path/to/your/repo"]
    }
  }
}
```

#### Example Queries

Once connected, ask your AI assistant:

- "Find all references to `OllamaLLMService`"
- "Show me the call hierarchy for `ChatAsync`"
- "What classes implement `IASTServiceClient`?"
- "Show dependencies for `GraphIndexService`"
- "Search for 'message composition' in the codebase"

See [codeMRI.MCP/README.md](codeMRI.MCP/README.md) for detailed documentation.

## 🏗️ Architecture

codeMRI follows a clean architecture pattern with the following components:

### Core Projects

- **codeMRI.Core**: Domain models, interfaces, and core business logic
- **codeMRI.Agents**: LLM orchestration and multi-agent documentation generation
- **codeMRI.Infrastructure**: External service implementations (Ollama, Qdrant, Neo4j, SQLite)
- **codeMRI.Visualization**: Graph and dependency visualization services
- **codeMRI.Server**: ASP.NET Core backend API
- **codeMRI.Frontend**: Blazor WebAssembly frontend
- **codeMRI.CLI**: Command-line interface for headless operations
- **codeMRI.ASTService**: Node.js-based AST parsing service
- **codeMRI.MCP**: Model Context Protocol server for AI assistant integration

### External Services

- **Ollama**: LLM inference and embeddings
- **Qdrant**: Vector database for semantic search
- **Neo4j**: Graph database for dependency relationships
- **SQLite**: Metadata and wiki content storage

## 📋 Prerequisites

1. **Docker & Docker Compose** (for infrastructure services)
2. **Ollama** installed and running locally
3. **.NET 8/9 SDK** installed
4. **Node.js 18+** (for AST service)

### Required Ollama Models

Pull the necessary models:

```bash
# Core models
ollama pull nomic-embed-text           # Embeddings
ollama pull mistral-large-3:675b-cloud  # Documentation & chat

# Judge models for multi-perspective evaluation
ollama pull kimi-k2-thinking:cloud
ollama pull deepseek-v3.1:671b-cloud
ollama pull cogito-2.1:671b-cloud
ollama pull minimax-m2:cloud

# Specialized routing models (optional but recommended)
ollama pull glm-4.6:cloud              # Code analysis
ollama pull ministral-3:14b-cloud       # Natural language
ollama pull devstral-2:123b-cloud       # Ensemble generation
```

> **Note**: The judge models above are used for multi-perspective documentation evaluation. Model routing allows task-specific model selection for optimal results. You can configure different models in `appsettings.json`.

## 🚀 Getting Started

### 1. Start Infrastructure Services

Start the required Docker services (AST service, Qdrant, Neo4j, SQLite):

```bash
docker-compose up -d
```

Verify services are running:

```bash
docker-compose ps
```

You should see:

- **code-mri-ast-service** on port 3000 (mapped from container port 3002)
- **code-mri-qdrant** on ports 6333 (HTTP) and 6334 (gRPC)
- **code-mri-neo4j** on ports 7474 (HTTP) and 7687 (Bolt)
- **code-mri-sqlite** (volume mount for database)
- **code-mri-mcp-server** on port 8080 (Streamable HTTP MCP transport)

### 2. Start the Backend API

Open a terminal and run:

```bash
cd codeMRI.Server
dotnet run
```

The API will be available at `http://localhost:5000`.

### 3. Start the Frontend

Open a new terminal and run:

```bash
cd codeMRI.Frontend
dotnet run
```

The browser should open automatically to the Blazor WebAssembly app. If not, navigate to the URL shown in the console (typically `http://localhost:5168`).

## 📖 Usage

### Web Interface

#### Ingesting a Repository

1. **Navigate to the Ingestion Page**: Click "Ingest Repository" in the navigation
2. **Enter Repository Source**:
   - For local repositories: Enter the full path (e.g., `/Users/username/projects/my-repo`)
   - For remote repositories: Enter the Git URL (e.g., `https://github.com/username/repo.git`)
3. **Click "Ingest"**: The system will:
   - Clone/analyze the repository
   - Parse source files using the AST service
   - Generate embeddings via Ollama
   - Build dependency graphs in Neo4j
   - Store metadata in SQLite

#### Generating Documentation

1. **Navigate to Repository Landing**: Select your ingested repository
2. **Select Target Audience**: Choose Developer, Tester, or DevOps for audience-specific documentation
3. **Click "Generate Advanced Wiki"**: The AI orchestrator will:
   - Perform hierarchical decomposition with semantic clustering
   - Apply intelligent model routing for optimal results
   - Generate documentation rubrics
   - Draft content using multiple LLM perspectives
   - Evaluate and synthesize final documentation using ensemble methods
   - Apply parent-child revision loops for consistency
4. **View Generated Wiki**: Browse the structured, audience-tailored documentation in the wiki interface

#### RAG Chat

1. **Navigate to the Chat Interface**: Access the chat page for your repository
2. **Ask Questions**: Query the codebase using natural language
3. **Semantic Search**: The system retrieves relevant code snippets using vector similarity and provides context-aware answers

#### Re-ingesting a Repository

1. **Navigate to Repository Landing**
2. **Click "Re-ingest"**: The dialog will pre-fill with the original repository source
3. **Confirm**: The system will update the analysis with the latest code changes

### Command-Line Interface (CLI)

The CLI provides a headless interface for automated documentation generation and integration into CI/CD pipelines.

#### Basic Usage

```bash
cd codeMRI.CLI
dotnet run -- --input /path/to/repo
```

#### Advanced Examples

```bash
# Generate documentation for a local repository with specific audience
dotnet run -- \
  --input /path/to/repo \
  --audience Developer \
  --server http://localhost:5247 \
  --verbose

# Process a Git repository and save output to files
dotnet run -- \
  --input https://github.com/username/repo.git \
  --audience Tester \
  --output ./docs \
  --force

# Full example with all options
dotnet run -- \
  --input /Users/username/projects/my-repo \
  --server http://localhost:5247 \
  --audience DevOps \
  --output ./generated-docs \
  --force \
  --verbose
```

#### CLI Options

| Option | Short | Description | Default |
| ------ | ----- | ----------- | ------- |
| `--input` | `-i` | Path to local repository or Git URL | (required) |
| `--server` | `-s` | URL of the codeMRI Server | `http://localhost:5247` |
| `--audience` | `-a` | Target audience (Developer, Tester, DevOps) | `Developer` |
| `--output` | `-o` | Directory to save generated Markdown files | (optional) |
| `--force` | `-f` | Force regeneration (ignore cache) | `false` |
| `--verbose` | `-v` | Enable verbose logging | `false` |

#### CLI Features

- **Real-time Progress**: Connects to SignalR hub for live generation updates
- **Git Integration**: Automatically clones remote repositories
- **Audience-specific Output**: Saves documentation to audience-named subdirectories
- **Flexible Output**: Either persist to database or export as Markdown files
- **Error Handling**: Comprehensive error messages and connection diagnostics

## ⚙️ Configuration

### Backend Configuration

Edit `codeMRI.Server/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "WikiDb": "Data Source=../data/sqlite/codemri.db",
    "IngestionDb": "Data Source=../data/sqlite/codemri.db"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "EmbeddingModel": "nomic-embed-text",
    "DocumentationModel": "mistral-large-3:675b-cloud",
    "ChatModel": "mistral-large-3:675b-cloud",
    "ContextSize": 16000,
    "JudgeModels": [
      "mistral-large-3:675b-cloud",
      "kimi-k2-thinking:cloud",
      "deepseek-v3.1:671b-cloud",
      "cogito-2.1:671b-cloud",
      "minimax-m2:cloud"
    ],
    "ModelRouting": {
      "EnableModelRouting": true,
      "EnableEnsembleGeneration": true,
      "CodeAnalysisModel": "glm-4.6:cloud",
      "NaturalLanguageModel": "ministral-3:14b-cloud",
      "SynthesisJudgeModel": "cogito-2.1:671b-cloud",
      "EnsembleModels": [
        "deepseek-v3.1:671b-cloud",
        "devstral-2:123b-cloud",
        "kimi-k2-thinking:cloud"
      ],
      "MinimumAgreementThreshold": 2
    }
  },
  "ASTService": {
    "BaseUrl": "http://localhost:3000",
    "TimeoutSeconds": 30,
    "Enabled": true
  },
  "Delegation": {
    "EnableDelegation": true,
    "MaxComplexityScore": 100,
    "MaxDelegationDepth": 3,
    "SemanticDiversityThreshold": 0.6,
    "ContextUtilizationRatio": 0.8
  }
}
```

#### Configuration Options

**Model Routing** (`Ollama.ModelRouting`):

- `EnableModelRouting`: Use specialized models for different tasks (code vs. natural language)
- `EnableEnsembleGeneration`: Generate multiple drafts and synthesize for higher quality
- `CodeAnalysisModel`: Model optimized for analyzing code structure and relationships
- `NaturalLanguageModel`: Model optimized for generating readable documentation
- `SynthesisJudgeModel`: Model that evaluates and combines outputs from multiple models
- `EnsembleModels`: List of models used in ensemble generation
- `MinimumAgreementThreshold`: Number of models that must agree for consensus (2-5)

**Delegation** (`Delegation`):

- `EnableDelegation`: Enable adaptive complexity-based delegation for scalability
- `MaxComplexityScore`: Complexity threshold that triggers delegation (50-200)
- `MaxDelegationDepth`: Maximum nesting level for delegated subtasks (1-5)
- `SemanticDiversityThreshold`: Minimum semantic difference for splitting modules (0.3-0.9)
- `ContextUtilizationRatio`: Target context window utilization ratio (0.6-0.9)

### Docker Services Configuration

Edit `docker-compose.yml` to adjust resource limits, ports, or environment variables for:

- **AST Service** (port 3000 → container 3002)
  - Memory limit: 1GB
  - Health check endpoint: `/health`
  - Built from `./codeMRI.ASTService/Dockerfile`
- **Qdrant** (ports 6333 HTTP, 6334 gRPC)
  - Memory limit: 2GB
  - Storage: `./data/qdrant`
  - Health check: `/healthz`
- **Neo4j** (ports 7474 HTTP, 7687 Bolt)
  - Memory limit: 3GB (heap: 512MB-2GB, pagecache: 512MB)
  - Credentials: `neo4j/changeme`
  - APOC plugin enabled
- **SQLite** (volume: `./data/sqlite`)
  - Lightweight Alpine container
  - Persistent storage for wiki and ingestion data
- **MCP Server** (port 8080)
  - Memory limit: 1GB
  - Streamable HTTP transport for AI assistants
  - Hybrid update strategy (FileSystemWatcher + polling fallback)
  - Connects to AST service for multi-language parsing
  - Index storage: `./data/codemri-index`

## 🧪 Testing

codeMRI follows Test-Driven Development (TDD) principles with comprehensive test coverage across all layers.

### Test Projects

- **codeMRI.Core.Tests**: Unit tests for domain models, interfaces, and core business logic
- **codeMRI.Agents.Tests**: Tests for LLM orchestration and multi-agent workflows
- **codeMRI.Infrastructure.Tests**: Integration tests for external services (Ollama, Qdrant, Neo4j)
- **codeMRI.Visualization.Tests**: Tests for graph and dependency visualization
- **codeMRI.Server.Tests**: API endpoint and server integration tests
- **codeMRI.Frontend.Tests**: Frontend component and service tests
- **codeMRI.E2E**: End-to-end tests for complete workflows

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test projects
dotnet test codeMRI.Core.Tests
dotnet test codeMRI.Agents.Tests
dotnet test codeMRI.Infrastructure.Tests
dotnet test codeMRI.Visualization.Tests
dotnet test codeMRI.Server.Tests
dotnet test codeMRI.Frontend.Tests

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Coverage

The test suite includes:

- Unit tests for all core services and strategies
- Integration tests for external service interactions
- Message composition strategy tests (Direct, Chunking)
- Context window validation tests
- Citation rendering and markdown processing tests
- RAG evaluation and synthesis tests

## 📚 Project Structure

```text
codeMRI/
├── codeMRI.Core/              # Domain models and interfaces
├── codeMRI.Agents/            # LLM orchestration and agents
├── codeMRI.Infrastructure/    # External service implementations
├── codeMRI.Visualization/     # Graph and visualization services
├── codeMRI.Server/            # ASP.NET Core backend
├── codeMRI.Frontend/          # Blazor WebAssembly frontend
├── codeMRI.CLI/               # Command-line interface
├── codeMRI.ASTService/        # Node.js AST parsing service
├── codeMRI.*.Tests/           # Test projects
├── data/                      # SQLite, Qdrant, and Neo4j data
├── docker-compose.yml         # Infrastructure services
└── README.md
```

## 🆕 Recent Improvements

### Context Window Management (December 2024)

- **Chunking Message Strategy**: Implemented iterative processing for large content that exceeds context windows
  - Automatic chunking with LLM-based processing
  - Prompt reformulation for each chunk with accumulated findings
  - Intelligent result synthesis combining all chunk outputs
- **Context Validation**: Automatic validation to prevent `Messages exceed context window` errors
  - Pre-flight checks before LLM calls
  - Dynamic buffer allocation for response generation
  - Proper accounting for system prompts, content, and response space

### Interactive Citations (December 2024)

- **Styled Citation Markers**: RAG responses now include visually appealing citation badges
  - Hover popovers showing source document details
  - File path, line numbers, and code snippets
  - Dark-themed, modern UI design
- **Citation Processing Pipeline**: Robust markdown processing with citation injection
  - Regex-based citation marker detection
  - HTML badge generation with tooltip content
  - Seamless integration with Markdig rendering

### Enhanced Testing (December 2024)

- **Comprehensive Test Coverage**: Expanded test suite across all projects
  - Server integration tests (`codeMRI.Server.Tests`)
  - Frontend component tests (`codeMRI.Frontend.Tests`)
  - E2E workflow tests (`codeMRI.E2E`)
- **TDD Workflow**: Strict adherence to Test-Driven Development principles
  - Red-Green-Refactor cycle for all changes
  - Generic-to-specific test progression
  - High code quality and maintainability

### MCP Server (December 2024)

- **Model Context Protocol Integration**: Full MCP server implementation for AI assistants
  - 7 query tools: find_references, call_hierarchy, find_implementations, query_dependencies, type_hierarchy, semantic_search, refresh_graph
  - Smart query gating prevents stale results during indexing
  - Multi-language support via Roslyn (C#) and tree-sitter (JS/Python/Go/Rust/Java)
- **Update Strategies**: 4 change detection mechanisms
  - Event-driven: Real-time updates via HTTP API
  - Polling: Periodic file system scanning
  - Hybrid: FileSystemWatcher with automatic fallback
  - Manual: Explicit AI-controlled refresh
- **Streamable HTTP Transport**: Docker-compatible transport for containerized deployment
  - Enables integration with Antigravity and other MCP clients
  - Health check endpoints for monitoring
  - Network-accessible via docker-compose

## 🤝 Contributing

Contributions are welcome! Please ensure:

- All tests pass before submitting PRs
- Follow the existing code style and architecture patterns
- Update documentation for new features

## 📄 License

[Specify your license here]

## 🙏 Acknowledgments

This project is based on **CodeWiki: Automated Repository-Level Documentation at Scale**, implementing the research paper's approach to hierarchical repository documentation using LLM-based agents. Key implementations include:

- Hierarchical decomposition with semantic clustering
- Multi-agent documentation generation with judge models
- Adaptive delegation for scalability
- Parent-child revision loops for consistency

The implementation extends the research with additional features like audience-specific documentation, intelligent model routing, and ensemble synthesis.
