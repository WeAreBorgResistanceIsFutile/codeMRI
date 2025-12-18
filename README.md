# codeMRI

**codeMRI** is an advanced AI-powered code analysis and documentation platform that generates comprehensive, multi-perspective repository documentation using LLM-based agents and semantic analysis.

## 🚀 Features

- **Intelligent Repository Ingestion**: Supports both local and remote Git repositories
- **Multi-Language AST Parsing**: Deep code analysis via dedicated AST service (TypeScript/JavaScript, Python, C#, Java, Go, Rust, and more)
- **AI-Powered Documentation**: Generates comprehensive wiki documentation using LLM orchestration with multiple judge models
- **Semantic Search**: RAG-based chat interface for querying codebases
- **Hierarchical Decomposition**: Automatically structures repositories into logical modules and components
- **Dependency Graph Analysis**: Visualizes code relationships and dependencies
- **Agent Telemetry**: Tracks and monitors AI agent activities throughout the documentation generation process
- **Re-ingestion Support**: Update documentation as repositories evolve

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
ollama pull nomic-embed-text
ollama pull mistral-large-3:675b-cloud
ollama pull kimi-k2-thinking:cloud
ollama pull deepseek-v3.1:671b-cloud
ollama pull cogito-2.1:671b-cloud
ollama pull minimax-m2:cloud
```

> **Note**: The judge models listed above are used for multi-perspective documentation evaluation. You can configure different models in `appsettings.json`.

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
- **code-mri-ast-service** on port 3000
- **code-mri-qdrant** on ports 6333 (HTTP) and 6334 (gRPC)
- **code-mri-neo4j** on ports 7474 (HTTP) and 7687 (Bolt)
- **code-mri-sqlite** (volume mount for database)

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

### Ingesting a Repository

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

### Generating Documentation

1. **Navigate to Repository Landing**: Select your ingested repository
2. **Click "Generate Advanced Wiki"**: The AI orchestrator will:
   - Perform hierarchical decomposition
   - Generate documentation rubrics
   - Draft content using multiple LLM perspectives
   - Evaluate and synthesize final documentation
3. **View Generated Wiki**: Browse the structured documentation in the wiki interface

### RAG Chat

1. **Navigate to the Chat Interface**: Access the chat page for your repository
2. **Ask Questions**: Query the codebase using natural language
3. **Semantic Search**: The system retrieves relevant code snippets and provides context-aware answers

### Re-ingesting a Repository

1. **Navigate to Repository Landing**
2. **Click "Re-ingest"**: The dialog will pre-fill with the original repository source
3. **Confirm**: The system will update the analysis with the latest code changes

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
    "ContextSize": 16384,
    "JudgeModels": [
      "mistral-large-3:675b-cloud",
      "kimi-k2-thinking:cloud",
      "deepseek-v3.1:671b-cloud",
      "cogito-2.1:671b-cloud",
      "minimax-m2:cloud"
    ]
  },
  "ASTService": {
    "BaseUrl": "http://localhost:3002",
    "TimeoutSeconds": 30,
    "Enabled": true
  }
}
```

### Docker Services Configuration

Edit `docker-compose.yml` to adjust resource limits, ports, or environment variables for:
- AST Service (port 3000)
- Qdrant (ports 6333, 6334)
- Neo4j (ports 7474, 7687, credentials: `neo4j/changeme`)

## 🧪 Testing

Run the test suite:

```bash
# Run all tests
dotnet test

# Run specific test projects
dotnet test codeMRI.Core.Tests
dotnet test codeMRI.Agents.Tests
dotnet test codeMRI.Infrastructure.Tests
dotnet test codeMRI.Visualization.Tests
```

## 📚 Project Structure

```
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

## 🤝 Contributing

Contributions are welcome! Please ensure:
- All tests pass before submitting PRs
- Follow the existing code style and architecture patterns
- Update documentation for new features

## 📄 License

[Specify your license here]

## 🙏 Acknowledgments

Based on the CodeWiki research paper on automated repository-level documentation generation.
