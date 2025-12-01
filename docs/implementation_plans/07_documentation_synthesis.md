# Documentation Synthesis Enhancement Plan

## Overview
Implement the **Hierarchical Assembly** and **Bottom-Up Synthesis** flow to create cohesive repository-level documentation.

## Current State
- `WikiGenerationService` generates pages individually based on file content.
- No explicit flow to summarize child modules into parent module documentation.

## Gaps to Address
- **Missing Bottom-Up Flow:** Documentation is currently generated per-file/per-module in isolation (or random order). CodeWiki requires processing leaves first, then parents.
- **Parent Module Synthesis:** Parent modules (directories/namespaces) need to be documented by **synthesizing** the summaries of their children, not just reading `README.md`.

## Implementation Steps

### 1. Recursive Processing Order
- Update `DocumentationGenerationPipeline` to traverse the `ModuleTree` **Post-Order** (Leaves -> Root).
- Ensure child documentation is generated *before* the parent is processed.

### 2. Child Context Aggregation
- When processing a Parent Module:
    - Retrieve the generated descriptions/summaries of all immediate Children.
    - Retrieve the Dependency Graph for this scope.

### 3. Parent Page Generation
- **Prompt:** "You are documenting module X. Here are the summaries of its sub-modules A, B, C. Here is how they interact. Write an architectural overview of module X."
- Generate "Architectural Overview", "Key Features", and "Interaction Patterns" sections.

### 4. Global Registry (Cross-Reference Management)
- Maintain a `GlobalComponentRegistry` of all generated pages.
- Ensure agents can link to existing pages when mentioning other modules.

## Required Changes
- **Files:** `codeMRI.Agents/Services/DocumentationGenerationPipeline.cs`, `codeMRI.Core/Services/WikiGenerationService.cs`
- **Logic:** Switch from linear/parallel processing to **Post-Order Traversal**.

## Expected Outcomes
- High-level documentation accurately reflects the detailed implementation of sub-modules.
- "Zoom-out" effect: Root docs explain the system; Leaf docs explain the code.