# Multi-Agent Architecture Implementation Plan

## Overview
Enhance the existing agent system to support **recursive processing** and **dynamic delegation** based on code complexity metrics, as defined in the CodeWiki paper.

## Current State
- Basic `AgentCoordinator` and `BaseAgent` exist.
- `DelegationService` exists but logic is generic.
- Agents (`AnalyzerAgent`, `DocumenterAgent`, etc.) are scaffolded but lack deep logic.

## Gaps to Address (Based on CodeWiki Paper Analysis)
- **Metric-Based Delegation:** Agents currently lack complexity assessment (cyclomatic complexity, nesting depth, semantic diversity, context window utilization) - Key missing feature from CodeWiki's Algorithm 1
- **Recursive Workflow:** Missing the complete recursive module processing loop with dynamic delegation capabilities
- **Agent Specialization:** Missing SynthesizerAgent and ValidatorAgent required for full CodeWiki compliance
- **Workspace Tools:** Agents lack comprehensive tool access for cross-module context exploration
- **Module Tree Updates:** Dynamically updating the module tree during delegation is not implemented

## Implementation Steps

### 1. Implement CodeWiki-Aligned Delegation Criteria
- **Complexity Metrics:** Cyclomatic complexity, nesting depth (as per CodeWiki)
- **Semantic Diversity:** Measure functionally distinct subcomponents  
- **Context Window Utilization:** Track token limits and chunking requirements
- **Dynamic Thresholds:** Configurable thresholds as described in CodeWiki Section 3.2

### 2. Implement Dynamic Delegation as per CodeWiki Algorithm 1
- **Complete the delegation workflow:** Analyze -> Calculate complexity -> ShouldDelegate? -> UpdateModuleTree
- **Agent Decision Logic:** Implement the exact criteria from CodeWiki Section 3.2:
  - Code complexity metrics exceeding thresholds
  - Semantic diversity requiring specialized handling
  - Context window utilization exceeding bounds
- **Recursive Coordination:** Ensure AgentCoordinator can handle nested delegation requests

### 3. Recursive Task Management
- Update `AgentCoordinator` to handle `DelegationRequest` recursively.
- Ensure the `ModuleTree` is updated dynamically when delegation splits a module.

### 4. Complete Agent Specialization and Tools
- **Implement Missing Agents:** SynthesizerAgent for hierarchical assembly, ValidatorAgent for quality assessment
- **Agent Workspace Tools:** As per CodeWiki Section 3.2:
  - Complete source code access
  - Full module tree for cross-module understanding
  - Documentation workspace tools (view, create, edit operations)
  - Dependency graph traversal for contextual exploration

## Required Changes
- **Files:** `codeMRI.Agents/Agents/BaseAgent.cs`, `codeMRI.Agents/Services/AgentCoordinator.cs`, `codeMRI.Agents/Services/DelegationService.cs`
- **New Models:** `ComplexityMetrics`

## Expected Outcomes (CodeWiki Compliance)
- Full implementation of CodeWiki's recursive multi-agent processing (Algorithm 1)
- Dynamic delegation system that handles modules of any size while maintaining quality
- Cross-module coherence through intelligent reference management
- Scale to repositories of arbitrary size as demonstrated in CodeWiki experiments
- Prepare for integration with CodeWikiBench evaluation system
