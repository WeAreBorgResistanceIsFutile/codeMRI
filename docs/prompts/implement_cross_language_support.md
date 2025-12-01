You are an expert in Polyglot Systems. Your task is to implement the **Cross-Language Integration** between the C# backend and the Node.js AST Service.

## Context
We have a Node.js service (`codeMRI.ASTService`) using Tree-sitter to parse code. We need a C# client to consume this and normalize the data into a unified Dependency Graph, as per the CodeWiki paper.

## Objectives
1.  **Implement `codeMRI.Infrastructure/Services/ASTServiceClient.cs`**:
    - Use `HttpClient` to POST source code (or file paths) to the Node.js service.
    - Deserialize the JSON response into a `RawDependencyData` model.

2.  **Update `codeMRI.Core/Services/EnhancedDependencyGraphService.cs`**:
    - Implement `BuildGraphAsync`.
    - Call the `ASTServiceClient`.
    - **Normalization Logic:** Convert language-specific relationships (e.g., "extends", "imports", "calls") into a unified `Dependency` model with `Type = DependencyType.DependsOn`.
    - **Node Identification:** Ensure nodes are uniquely identified (e.g., `File:Class` or `Namespace.Class`).

## Constraints
- The C# code must be robust to network failures (Node service down).
- Handle the 7 supported languages: Python, Java, JS, TS, C, C++, C#.
- The output must be a `DependencyGraph` usable by the `HierarchicalDecompositionService`.

## Input Files
- `codeMRI.Core/Interfaces/IASTServiceClient.cs`
- `codeMRI.Core/Services/EnhancedDependencyGraphService.cs`
- `codeMRI.Core/Models/EnhancedDependencyGraph.cs`

Generate the C# code for the Client and the Service update.