# Visual Artifact Generation Plan

## Overview
Implement the "Multi-Modal Synthesis" capability of CodeWiki to generate diagrams (Architecture, Data Flow, Sequence) alongside text documentation.

## Current State
- `WikiGenerationService` generates text only.
- `codeMRI.Visualization` project exists but is empty/scaffolded.

## Gaps to Address
- **No Diagram Generation:** System cannot create visual representations of code structure.
- **No Text-Visual Integration:** Markdown output lacks embedded diagrams.

## Implementation Steps

### 1. Architecture Diagram Generation (Class/Component)
- **Input:** `ModuleTree` and `DependencyGraph`.
- **Process:**
    - Extract relationships (Inheritance, Dependency, Composition) from the AST/Dependency Graph.
    - Generate **Mermaid.js class diagram** syntax.
    - *Alternative:* Use LLM to summarize high-level architecture into Mermaid syntax if static analysis is too noisy.

### 2. Sequence Diagram Generation (Behavior)
- **Input:** Call Graph (from `EnhancedDependencyGraphService`).
- **Process:**
    - Identify "Entry Point" methods (public APIs).
    - Trace execution paths (static call graph).
    - Generate **Mermaid.js sequence diagram** syntax for key flows.

### 3. Data Flow Visualization
- **Input:** Variable usage and type propagation (if available from AST).
- **Process:**
    - Map data movement between modules.
    - Generate Mermaid flowchart or state diagram.

### 4. Integration with Documentation
- Update `WikiGenerationService` to call `DiagramGeneratorService`.
- Embed the generated Mermaid markdown blocks into the Wiki pages (e.g., ````mermaid ... ````).

## Required Changes
- **Project:** `codeMRI.Visualization`
- **New Service:** `DiagramGeneratorService`
- **Integration:** Call this service inside `WikiGenerationService.GeneratePageAsync`.

## Expected Outcomes
- Documentation pages include automatic diagrams showing class structure and call sequences.
- Visuals are kept in sync with code (re-generated on update).