# Codemri.Astservice Src

## 1. Overview (For Everyone)

The `Codemri.Astservice Src` module is a web service built with Node.js and Express. Its primary purpose is to provide a RESTful API for analyzing source code by generating its Abstract Syntax Tree (AST).

The service solves the problem of needing a centralized, remote capability to parse code without bundling parsing libraries into every client application. This is useful for tools like code editors, CI/CD pipelines, or static analysis platforms.

Its primary capabilities are:
*   Exposing a secure HTTP API for code analysis.
*   Accepting source code snippets and returning a structured AST representation.
*   Providing a health check endpoint for monitoring.
*   Including a standalone mock mode for development and testing, which simulates the API's behavior without performing actual parsing.

## 2. User Guide (For End Users)

### How to Use the Features

You interact with the AST Service by sending HTTP requests to its endpoints. The main feature is the code analysis endpoint.

**To analyze a piece of code:**

1.  Send a `POST` request to the `/ast/analyze` endpoint.
2.  The request body must be a JSON object containing two fields:
    *   `language`: A string specifying the programming language of the code (e.g., `"javascript"`, `"python"`).
    *   `code`: A string containing the actual source code to be analyzed.
3.  The service will respond with a JSON object containing the analysis results.

**Example Request:**
```json
POST /ast/analyze
Content-Type: application/json

{
  "language": "javascript",
  "code": "function add(a, b) { return a + b; }"
}
```

**Example Response (in mock mode):**
```json
{
  "success": true,
  "language": "javascript",
  "ast": {
    "type": "mock_ast",
    "message": "AST service is running in mock mode",
    "nodes": [],
    "edges": []
  },
  "metadata": {
    "lines": 1,
    "characters": 35,
    "timestamp": "2023-10-27T10:00:00.000Z"
  }
}
```

### Configuration Options (User-facing)

The service can be run in two distinct modes, determined by which entry point is executed:

*   **Mock Mode:** By running the `mock-app.js` file, the service operates in a mock mode. It will not perform real AST parsing but will return a predefined, static response. This is useful for testing client applications without needing the full parsing backend.
*   **Production Mode:** By running the `app.js` file, the service acts as a router for the actual analysis logic, which is handled by separate route modules.

### Common Use Cases

*   **Development & Testing:** Use the mock service (`mock-app.js`) to develop and test client applications that need to consume the AST API without a fully functional backend.
*   **Code Analysis Integration:** Integrate the production service (`app.js`) into a CI/CD pipeline to automatically analyze new code commits.
*   **IDE/Editor Plugin:** A code editor can send code to the service to get its AST for features like syntax highlighting, code navigation, or refactoring.

## 3. Technical Architecture (For Developers)

### Architectural Pattern

The application follows a modular routing pattern, a common architectural style for Express.js applications. The primary entry point (`app.js`) is responsible for setting up the Express application, middleware, and delegating request handling to separate route modules (`./routes/ast`, `./routes/analysis`, `./routes/health`). A separate, self-contained mock application (`mock-app.js`) is provided for development and testing.

### Metrics

*   **Cohesion:** 0,00
*   **Coupling:** 0,00
*   **Complexity:** 9,0

### Class/Component Structure and Key Relationships

The service is composed of two main entry points and relies on external route handlers.

*   **`mock-app.js`**: A standalone, self-contained Express server. It defines its own middleware and endpoints directly within the file. It does not depend on other route files.
*   **`app.js`**: The main entry point for the production service. It configures the Express application and uses `app.use()` to mount routes from external modules.
    *   It depends on `./routes/ast`, `./routes/analysis`, and `./routes/health` for handling API requests.
*   **Middleware**: Both applications use standard Express middleware:
    *   `helmet` for security headers.
    *   `cors` for enabling Cross-Origin Resource Sharing.
    *   `express.json()` and `express.text()` for parsing request bodies, with a payload limit of 50mb in `app.js`.

### Important Public Interfaces

The public interfaces are the HTTP endpoints exposed by the service.

*   **From `mock-app.js`:**
    *   `POST /ast/analyze`: Mock analysis endpoint.
    *   `GET /health`: Health check.
    *   `GET /`: Service information.
*   **From `app.js` (routes are delegated):**
    *   `GET /api/ast/*`: Handles requests related to AST operations.
    *   `GET /api/analysis/*`: Handles general analysis requests.
    *   `GET /health`: Health check.

### Mermaid Component Diagram

```mermaid
graph TD
    subgraph "Client Applications"
        Client[Client]
    end

    subgraph "AST Service"
        AppJS[app.js (Production)]
        MockAppJS[mock-app.js (Mock)]
        
        AppJS -->|delegates to| ASTRoutes[./routes/ast]
        AppJS -->|delegates to| AnalysisRoutes[./routes/analysis]
        AppJS -->|delegates to| HealthRoutes[./routes/health]
    end

    Client -->|HTTP Request| AppJS
    Client -->|HTTP Request| MockAppJS
```

## 4. Operations & Deployment (For DevOps)

### External Dependencies

No external service dependencies (e.g., databases, external APIs, message queues) were detected in the provided source files. The application relies only on npm packages (`express`, `cors`, `helmet`).

### Configuration

Configuration is managed via environment variables.

*   **`PORT`**: The TCP port on which the server will listen.
    *   In `mock-app.js`, it defaults to `3000` if not set.
    *   In `app.js`, it defaults to `3002` if not set.

### Troubleshooting and Logs

*   **Logging**: The application logs a single message to standard output upon successful startup: `AST Service running on port ${PORT}`. Check the console or container logs to verify the service has started and on which port.
*   **Error Handling**: The service uses standard HTTP status codes. For example, a `400 Bad Request` is returned if required fields are missing in the request body to the `/ast/analyze` endpoint.
*   **Troubleshooting Steps**:
    1.  Verify the service is running by checking the startup log message.
    2.  Ensure the correct port is being used by the client.
    3.  Check the `PORT` environment variable if the service is not running on the expected default port (3000 or 3002).
    4.  For `POST /ast/analyze`, ensure the request body is valid JSON and contains the `language` and `code` fields.

## 5. API Reference

### Endpoint: `POST /ast/analyze`

Analyzes a code snippet and returns its AST. (Documented from `mock-app.js`).

*   **Request Body**:
    ```json
    {
      "language": "string",
      "code": "string"
    }
    ```
*   **Success Response (200 OK)**:
    ```json
    {
      "success": true,
      "language": "string",
      "ast": { ... },
      "metadata": { ... }
    }
    ```
*   **Error Response (400 Bad Request)**:
    ```json
    {
      "error": "Missing required fields: language and code"
    }
    ```

### Endpoint: `GET /health`

A simple health check to verify the service is running.

*   **Success Response (200 OK)**:
    ```json
    {
      "status": "healthy",
      "timestamp": "string"
    }
    ```

### Endpoint: `GET /`

Provides basic information about the running service instance. (Available in `mock-app.js`).

*   **Success Response (200 OK)**:
    ```json
    {
      "service": "codeMRI AST Service",
      "status": "running",
      "mode": "mock"
    }
    ```

### Endpoint: `GET /api/ast/*`

Base path for all AST-related API endpoints. The specific sub-paths and methods are defined in the external `./routes/ast` module.

### Endpoint: `GET /api/analysis/*`

Base path for all general analysis API endpoints. The specific sub-paths and methods are defined in the external `./routes/analysis` module.

<details>
<summary>Relevant source files</summary>

- [codeMRI.ASTService/src/mock-app.js](/var/folders/9d/5x7df5dx5zxcjnl4dbxzfqw00000gn/T/codeMRI_982cf0c472d443648edb9ca4f0587557/blob/main/codeMRI.ASTService/src/mock-app.js)
- [codeMRI.ASTService/src/app.js](/var/folders/9d/5x7df5dx5zxcjnl4dbxzfqw00000gn/T/codeMRI_982cf0c472d443648edb9ca4f0587557/blob/main/codeMRI.ASTService/src/app.js)
</details>
