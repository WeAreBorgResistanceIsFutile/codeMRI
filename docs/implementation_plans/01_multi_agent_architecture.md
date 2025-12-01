# Multi-Agent Architecture Implementation Plan

## Overview
Enhance the existing agent system to support **recursive processing** and **dynamic delegation** based on code complexity metrics, as defined in the CodeWiki paper.

## Current State
- Basic `AgentCoordinator` and `BaseAgent` exist.
- `DelegationService` exists but logic is generic.
- Agents (`AnalyzerAgent`, `DocumenterAgent`, etc.) are scaffolded but lack deep logic.

## Gaps to Address
- **Metric-Based Delegation:** Agents do not currently check code complexity (cyclomatic complexity, nesting depth) or token limits to decide when to delegate.
- **Recursive Workflow:** The specific loop of "Analyze -> Delegate if too complex -> Document" needs to be hardened in the `BaseAgent` or `Coordinator`.

## Implementation Steps

### 1. Enhance Analyzer for Metrics
- Update `AnalyzerAgent` (or `RoslynCSharpAnalyzer` / `ASTService`) to return:
    - Cyclomatic Complexity
    - Nesting Depth
    - Token Count
    - Function/Class Count

### 2. Implement Dynamic Delegation Logic
- Modify `BaseAgent.ShouldDelegate` to use the metrics above.
- **Thresholds:** Define configurable thresholds (e.g., `MaxTokensPerModule = 2000`, `MaxComplexity = 15`).
- **Strategy:**
    - If `TokenCount > Threshold`, split module.
    - If `Complexity > Threshold`, delegate sub-components to new agents.

### 3. Recursive Task Management
- Update `AgentCoordinator` to handle `DelegationRequest` recursively.
- Ensure the `ModuleTree` is updated dynamically when delegation splits a module.

### 4. Specialized Agent Tools
- Equip agents with explicit "Workspace Tools":
    - `ReadModuleContext` (Access to sibling/child module info)
    - `UpdateModuleDoc` (Write access)
    - `QueryDependencyGraph` (Context exploration)

## Required Changes
- **Files:** `codeMRI.Agents/Agents/BaseAgent.cs`, `codeMRI.Agents/Services/AgentCoordinator.cs`, `codeMRI.Agents/Services/DelegationService.cs`
- **New Models:** `ComplexityMetrics`

## Expected Outcomes
- Agents automatically break down large files/classes into smaller tasks.
- System scales to repositories of arbitrary size by keeping context windows managed.