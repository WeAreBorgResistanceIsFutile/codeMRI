# Codemri.Server

### 1. Overview (For Everyone)
The `Codemri.Server` module is the central backend component of the codeMRI application. It serves as a web API and a real-time communication hub, orchestrating interactions between various services and client applications.

The module solves the problem of coordinating complex code analysis tasks. It provides a single point of contact for clients to request analysis, manage data, and receive live updates.

Its primary capabilities include:
- Exposing a RESTful API for client interactions.
- Providing real-time updates to connected clients via SignalR.
- Integrating with external services for Language Model (LLM) interactions (Ollama) and Abstract Syntax Tree (AST) analysis.
- Persisting application data and job information in SQLite databases.

### 2. User Guide (For End Users)
This guide is intended for developers or systems interacting with the `Codemri.Server` as a client.

#### How to use the features
The server is designed to be consumed by client applications. Interaction happens in two main ways:
1.  **REST API**: Clients can send HTTP requests to the server's endpoints to perform actions such as querying data, submitting new analysis jobs, or managing configuration.
2.  **SignalR Hub**: Clients can establish a persistent connection to the `/wikiHub` endpoint to receive real-time notifications and data updates from the server without needing to poll the API.

#### Configuration options (user-facing)
The behavior of the server can be configured through the standard .NET configuration providers (e.g., `appsettings.json`, environment variables). The key configuration sections are:

-   **`Ollama`**: Contains settings for connecting to the Ollama Large Language Model service.
-   **`ASTService`**: Contains settings for connecting to the external Abstract Syntax Tree analysis service.
-   **`CodeWiki`**: Contains options related to the CodeWiki functionality.
-   **`ConnectionStrings`**:
    -   `WikiDb`: The connection string for the primary SQLite database. If not provided, a default file named `codemri.db` will be created in the user's local application data folder.
    -   `IngestionDb`: The connection string for the ingestion job database. If not provided, a default file named `ingestion.db` will be created in the user's local application data folder.

#### Common use cases
-   A client application starts by connecting to the `/wikiHub` to listen for real-time events.
-   To analyze a new codebase, the client makes an API call to an ingestion endpoint (specific endpoint not defined in this file).
-   The server processes the job in the background, using the configured AST and LLM services.
-   As progress is made or results are available, the server pushes notifications to the client via the SignalR hub.
-   The client can also query the API at any time to get the current state of the wiki or job status.

### 3. Technical Architecture (For Developers)
The `Codemri.Server` is built on the ASP.NET Core framework and follows a Dependency Injection (DI) based composition root pattern.

-   **Architectural Pattern**: Dependency Injection-based Web API
-   **Metrics**: Cohesion (1,00), Coupling (1,00), Complexity (1,0)
-   **Class/Component structure and key relationships**:
    -   `Program.cs` acts as the composition root, configuring and building the web application.
    -   It registers core ASP.NET services like Controllers, SignalR, and Swagger.
    -   Custom services are registered as singletons:
        -   `OllamaLLMService` implements `ILLMClient` for LLM interactions.
        -   `ASTServiceClient` implements `IASTServiceClient` for AST analysis.
        -   `SqliteWikiRepository` implements `IWikiRepository` for data persistence.
        -   `DbIngestionManager` implements `IIngestionJobManager` for background job management.
    -   The `WireUp.Registered()` method registers additional services from the `codeMRI.Core` project.
    -   Controllers and the `WikiHub` depend on these injected services to perform their work.

-   **Important public interfaces**:
    -   `codeMRI.Core.Interfaces.ILLMClient`: Defines the contract for interacting with a language model.
    -   `codeMRI.Core.Interfaces.IASTServiceClient`: Defines the contract for communicating with the AST service.
    -   `codeMRI.Core.Interfaces.IWikiRepository`: Defines the contract for reading and writing wiki data.
    -   `codeMRI.Agents.Services.IIngestionJobManager`: Defines the contract for managing ingestion jobs.

#### Mermaid Component Diagram
```mermaid
graph TD
    subgraph "Codemri.Server"
        A[Program.cs] --> B[Web Application]
        B --> C[API Controllers]
        B --> D[WikiHub SignalR]
        
        A --> E[Service Collection]
        E --> F[OllamaLLMService: ILLMClient]
        E --> G[ASTServiceClient: IASTServiceClient]
        E --> H[SqliteWikiRepository: IWikiRepository]
        E --> I[DbIngestionManager: IIngestionJobManager]
        E --> J[WireUp.Registered Services]

        C --> F
        C --> G
        C --> H
        C --> I
        D --> I
    end

    subgraph "External Dependencies"
        K[Ollama API]
        L[AST Service]
        M[SQLite DB (WikiDb)]
        N[SQLite DB (IngestionDb)]
    end

    F -- HTTP Calls --> K
    G -- HTTP Calls --> L
    H -- Reads/Writes --> M
    I -- Reads/Writes --> N
```

### 4. Operations & Deployment (For DevOps)
This section provides details for deploying and maintaining the `Codemri.Server`.

-   **External dependencies**:
    -   **Ollama Service**: An instance of the Ollama LLM server must be running and accessible via the network. The connection details are configured in the `Ollama` settings section.
    -   **AST Service**: An external service for Abstract Syntax Tree analysis must be running and accessible. The connection details are configured in the `ASTService` settings section.

-   **Configuration**:
    -   The application is configured via .NET configuration files (e.g., `appsettings.json`) or environment variables.
    -   **Key Settings**:
        -   `Ollama`: Configuration for the LLM service endpoint.
        -   `ASTService`: Configuration for the AST analysis service endpoint.
        -   `CodeWiki`: Configuration for the CodeWiki feature.
        -   `ConnectionStrings:WikiDb`: Path or full connection string for the main data database. Defaults to `%LOCALAPPDATA%/codeMRI/codemri.db`.
        -   `ConnectionStrings:IngestionDb`: Path or full connection string for the job management database. Defaults to `%LOCALAPPDATA%/codeMRI/ingestion.db`.
    -   **Logging**: Configured using Serilog. By default, logs are written to the application's console output.

-   **Troubleshooting and Logs**:
    -   **Logs**: All application logs are output to the console. Check the standard output and standard error streams of the running process for diagnostic information.
    -   **API Issues**: In a development environment, Swagger UI is available at the `/swagger` endpoint. It can be used to inspect and test API endpoints directly.
    -   **Database Issues**: Ensure the application has file system permissions to create and write to the configured SQLite database paths (e.g., the `%LOCALAPPDATA%` directory).
    -   **External Service Issues**: Verify network connectivity and configuration for the Ollama and AST services. The server will log errors related to failed HTTP requests to these services.

### 5. API Reference (If applicable)
The `Codemri.Server` exposes a RESTful API and a SignalR Hub for client interaction.

-   **REST API**:
    -   The application exposes a set of API endpoints through ASP.NET Core controllers.
    -   The complete and up-to-date API reference, including all endpoints, HTTP methods, request/response models, and required parameters, is available via the built-in Swagger UI.
    -   **Accessing the API Reference**: In a development environment, navigate to `/swagger` in your browser to explore the interactive API documentation.

-   **SignalR Hub**:
    -   The server exposes a SignalR Hub at the `/wikiHub` endpoint.
    -   Clients can connect to this hub to receive real-time messages and data pushed from the server.
    -   The specific events and data contracts for the hub are defined in the `codeMRI.Server.Hubs.WikiHub` class (not shown in this file).

<details>
<summary>Relevant source files</summary>

- [codeMRI.Server/Program.cs](/var/folders/9d/5x7df5dx5zxcjnl4dbxzfqw00000gn/T/codeMRI_982cf0c472d443648edb9ca4f0587557/blob/main/codeMRI.Server/Program.cs)
</details>
