# Cross-Language Support Implementation Plan

## Overview
Leverage the existing Node.js `ASTService` (Tree-sitter) to provide normalized, cross-language dependency analysis for the C# backend.

## Current State
- `codeMRI.ASTService` (Node.js) has dependencies for 7 languages (Python, Java, JS, TS, C, C++, C#).
- `codeMRI.Core/Interfaces/IASTServiceClient.cs` exists.

## Gaps to Address
- **Unified Dependency Model:** The C# backend needs to normalize the raw AST data from Tree-sitter into a unified `depends_on` graph as described in the CodeWiki paper.
- **Edge Normalization:** Explicitly mapping "Inherits", "Calls", "Imports" to a generic dependency graph for the decomposition algorithm.

## Implementation Steps

### 1. AST Service Client (C#)
- Implement `ASTServiceClient` to communicate with the Node.js service.
- **Endpoints:** Call `/parse` or `/analyze` on the Node.js service.

### 2. Unified Dependency Normalization
- **Input:** Raw JSON AST/Dependency data from Node.js.
- **Process:**
    - Map language-specific constructs (e.g., Java `extends`, Python `import`, C# `using`) to a single `DependencyType.DependsOn`.
    - Extract "Zero-In-Degree" components (Entry Points) as per the paper.

### 3. Language-Specific Strategies
- Define strategies for identifying "Modules" in each language:
    - Python: Files/Directories.
    - Java: Classes/Packages.
    - C/C++: Header/Implementation files.

## Required Changes
- **Files:** `codeMRI.Core/Services/EnhancedDependencyGraphService.cs` (to use the client), `codeMRI.Infrastructure/Services/ASTServiceClient.cs`.
- **Node.js Service:** Ensure `codeMRI.ASTService` returns structured dependency data, not just raw ASTs.

## Expected Outcomes
- A single, language-agnostic `DependencyGraph` object in C# that powers the `HierarchicalDecompositionService`.