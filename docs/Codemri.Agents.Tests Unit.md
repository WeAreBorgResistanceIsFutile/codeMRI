# Codemri.Agents.Tests Unit

### 1. Overview (For Everyone)
The `Codemri.Agents.Tests Unit` module is a collection of unit tests designed to ensure the reliability, correctness, and robustness of the core components within the `codeMRI.Agents` library. It validates the fundamental behaviors of the agent-based system, providing a safety net for developers and confirming that the system's logic works as intended under various conditions.

**Key problems it solves:**
*   **Ensures System Reliability:** By rigorously testing each component, the module prevents regressions and ensures that new changes do not break existing functionality.
*   **Validates Delegation Logic:** It verifies that the system's intelligent delegation mechanism correctly identifies tasks that are too complex and routes them appropriately, preventing agent overload or failure.
*   **Confirms Pipeline Orchestration:** It tests that the complex `DocumentationGenerationPipeline` correctly sequences calls to multiple services to produce a cohesive final output.
*   **Verifies Task Coordination:** It ensures the central `AgentCoordinator` can correctly route tasks to the right agents and manage multi-level delegation chains.

**Primary capabilities:**
*   Unit testing of the `BaseAgent` class, including task handling, complexity calculation, and delegation decision-making.
*   Unit testing of the `AgentCoordinator` service, focusing on task routing, execution, and delegation flow management.
*   Unit testing of the `DocumentationGenerationPipeline` to validate its end-to-end workflow for generating documentation artifacts.

### 2. User Guide (For End Users)
The "end users" of this test module are developers working on the `codeMRI.Agents` library. These tests are run using a standard .NET test runner (e.g., Visual Studio Test Explorer, `dotnet test` CLI command, NUnit Console) to validate code changes.

**How to use the features:**
The features are the tests themselves. To execute them, navigate to the root of the test project directory in a terminal and run the command:
```bash
dotnet test
```
This command will discover and run all tests in the module, reporting success or failure.

**Configuration options (user-facing):**
This test module does not have direct user-facing configuration. However, it tests components that are configurable. For example, `AgentCoordinatorTests` uses mocked `AgentSettings` to simulate different operational environments, testing how the coordinator behaves with settings like `MaxRecursionDepth` and `ComplexityThresholds`. These settings are typically managed in the main application's configuration files (e.g., `appsettings.json`).

**Common use cases:**
*   **Before Committing Code:** A developer modifies the delegation logic in `BaseAgent` and runs the `BaseAgentTests` to ensure the changes work as expected and don't introduce side effects.
*   **Validating New Features:** A new step is added to the `DocumentationGenerationPipeline`. A developer updates `DocumentationGenerationPipelineTests` to include assertions for the new step, ensuring it integrates correctly with the existing flow.
*   **Pre-release Validation:** As part of a continuous integration (CI) pipeline, the full test suite is run automatically to guarantee that a new build is stable and ready for release.

### 3. Technical Architecture (For Developers)
This module is a standard NUnit test project that follows the Arrange-Act-Assert pattern. It uses Moq for mocking dependencies and Microsoft.Extensions.Logging.Abstractions for logging, ensuring tests are isolated and repeatable.

**Architectural Pattern:**
The module itself does not implement a specific architectural pattern but is designed to test a system that employs several patterns:
*   **Chain of Responsibility:** Evident in the `AgentCoordinator` which passes tasks to agents until one can handle them.
*   **Pipeline/Orchestration:** The `DocumentationGenerationPipeline` orchestrates a sequence of calls to various services to achieve a complex goal.
*   **Dependency Injection:** Heavily used throughout the system under test and replicated in the test setup via constructor injection.

**Metrics:**
*   **Cohesion (0,00):** The cohesion is very low, which is expected and appropriate for a test module. Its purpose is to test many disparate, unrelated components of the `codeMRI.Agents` library.
*   **Coupling (1,00):** The coupling is very high. The test module is tightly bound to the implementation details of the classes it tests (e.g., `BaseAgent`, `AgentCoordinator`). This is necessary and desirable for unit testing to validate specific behaviors.
*   **Complexity (42,0):** The complexity score reflects the non-trivial logic being tested, such as conditional delegation rules, recursive task handling, and multi-step pipeline execution.

**Class/Component structure and key relationships:**
The module is organized into test classes, each targeting a primary component from the main library:
*   `BaseAgentTests`: Validates the functionality of the `BaseAgent` abstract class.
*   `AgentCoordinatorTests`: Tests the `AgentCoordinator` service.
*   `DocumentationGenerationPipelineTests`: Verifies the `DocumentationGenerationPipeline` service.
*   `DelegationLogicTests`: A focused test on the delegation behavior, using a `TestAgent` derived from `BaseAgent`.

These test classes use mocks (e.g., `Mock<IASTServiceClient>`, `Mock<ILogger>`) to simulate the dependencies of the components under test, ensuring that each test operates in a controlled environment.

**Important public interfaces:**
The tests interact with and validate the contracts of several key interfaces from the main library:
*   `IAgent`: Defines the core contract for an agent with methods like `CanHandle`, `ShouldDelegate`, and `ExecuteAsync`.
*   `IAgentCoordinator`: The main interface for task coordination with the `CoordinateTaskAsync` method.
*   `IASTServiceClient`: Provides code parsing and analysis capabilities.
*   `IWikiGenerationService`, `IVisualSynthesisService`, etc.: Service interfaces used by the documentation generation pipeline.

**Mermaid Component Diagram (graph TD
    *   `codeMRI.Agents.Tests.Unit`:
    *   `BaseAgentTests`: Validates the functionality of the `BaseAgent` abstract class.
    *   `AgentCoordinatorTests`: Tests the `AgentCoordinator` service.
    *   `DocumentationGenerationPipelineTests`: Verifies the `DocumentationGenerationPipeline` service.
    *   `DelegationLogicTests`: A focused test on the delegation behavior, using a `TestAgent` derived from `BaseAgent`.

These test classes use mocks (e.g., `Mock<IASTServiceClient>`, `Mock<ILogger>`) to simulate the dependencies of the components under test, ensuring that each test operates in a controlled environment.

**Important public interfaces:**
The tests interact with and validate the contracts of several key interfaces from the main library:
*   `IAgent`: Defines the core contract for an agent with methods like `CanHandle`, `ShouldDelegate`, and `ExecuteAsync`.
*   `IAgentCoordinator`: The main interface for task coordination with the `CoordinateTaskAsync` method.
*   `IASTServiceClient`: Provides code parsing and analysis capabilities.
*   `IWikiGenerationService`, `IVisualSynthesisService`, etc.: Service interfaces used by the documentation generation pipeline.

**Mermaid Component Diagram (graph TD):**
```mermaid
graph TD
    subgraph "Codemri.Agents.Tests Unit"
        A[DelegationLogicTests]
        B[BaseAgentTests]
        C[AgentCoordinatorTests]
        D[DocumentationGenerationPipelineTests]
    end

    subgraph "codeMRI.Agents (System Under Test)"
        BaseAgent[BaseAgent]
        AgentCoordinator[AgentCoordinator]
        DocGenPipeline[DocumentationGenerationPipeline]
        TestAgent[TestAgent : BaseAgent]
    end

    subgraph TD
    *   `DelegationLogicTests`: Tests the `DelegationLogicTests`).
    *   `DelegationLogicTests`: `DelegationLogicTests`

<details>
<summary>Relevant source files</summary>

- [codeMRI.Agents.Tests/Unit/DelegationLogicTests.cs](/var/folders/9d/5x7df5dx5zxcjnl4dbxzfqw00000gn/T/codeMRI_982cf0c472d443648edb9ca4f0587557/blob/main/codeMRI.Agents.Tests/Unit/DelegationLogicTests.cs)
- [codeMRI.Agents.Tests/Unit/DocumentationGenerationPipelineTests.cs](/var/folders/9d/5x7df5dx5zxcjnl4dbxzfqw00000gn/T/codeMRI_982cf0c472d443648edb9ca4f0587557/blob/main/codeMRI.Agents.Tests/Unit/DocumentationGenerationPipelineTests.cs)
- [codeMRI.Agents.Tests/Unit/BaseAgentTests.cs](/var/folders/9d/5x7df5dx5zxcjnl4dbxzfqw00000gn/T/codeMRI_982cf0c472d443648edb9ca4f0587557/blob/main/codeMRI.Agents.Tests/Unit/BaseAgentTests.cs)
- [codeMRI.Agents.Tests/Unit/AgentCoordinatorTests.cs](/var/folders/9d/5x7df5dx5zxcjnl4dbxzfqw00000gn/T/codeMRI_982cf0c472d443648edb9ca4f0587557/blob/main/codeMRI.Agents.Tests/Unit/AgentCoordinatorTests.cs)
</details>
