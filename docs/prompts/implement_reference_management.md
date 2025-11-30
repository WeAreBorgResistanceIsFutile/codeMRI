# LLM Prompt: Implement Reference Management

## Context
Create a global reference system for cross-module documentation in CodeWiki.

## Implementation Plan
Based on `docs/implementation_plans/05_reference_management.md`:

1. Reference registry
2. Cross-reference system
3. Navigation features
4. Versioning support

## Task
Generate code for:

### Step 1: Reference Registry
- Global component registry
- Reference resolution
- Link validation

### Step 2: Cross-References
- Reference generation
- Link maintenance
- Integrity checks

### Step 3: Navigation
- Documentation navigation
- Breadcrumb system
- Search index

### Step 4: Versioning
- Reference versioning
- Change tracking
- Migration system

## Constraints
- C# 10+ backend
- Follow existing patterns
- Add documentation and tests

## Expected Output
- ReferenceManager service
- Link validation
- Navigation features
- Test coverage

## Example
```csharp
public class ReferenceManager
{
    public void RegisterReference(string id, ReferenceInfo info) { ... }
    public ReferenceInfo ResolveReference(string id) { ... }
    public bool ValidateLinks() { ... }
}
