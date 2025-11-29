# coreMRI

A C#/.NET implementation of the DeepWiki AI documentation generator using Ollama.

## Prerequisites

1. **Ollama** installed and running.
2. Models pulled:
   ```bash
   ollama pull nomic-embed-text
   ollama pull llama3
   ```
3. **.NET 8/9 SDK** installed.

## Running the Project

### 1. Start the Backend API~~~~
Open a terminal and run:
```bash
cd DeepWiki.Api
dotnet run --urls=http://localhost:5000
```
The API will be available at `http://localhost:5000`.
Swagger UI: `http://localhost:5000/swagger`

### 2. Start the Frontend
Open a new terminal and run:
```bash
cd DeepWiki.Web
dotnet run
```
The browser should open automatically. If not, check the console output for the URL (e.g., `http://localhost:5168`).

## Usage

1. **Ingest**: Enter the full local path to the repository you want to document (e.g., `/Users/username/projects/my-repo`) in the "Repository Path" field and click "Ingest Repo". This processes files and generates embeddings via Ollama.
2. **Structure**: Click "Generate Structure" to analyze the file tree and create a Wiki navigation structure.
3. **View Pages**: Click on any page link in the sidebar to generate and view the content.
4. **Chat**: Use the RAG Chat at the bottom to ask questions about the codebase.

## Configuration

Backend configuration is in `DeepWiki.Api/appsettings.json`. You can change the Ollama URL or models there.
Frontend API URL is currently hardcoded to `http://localhost:5000` in `DeepWiki.Web/Program.cs`.
