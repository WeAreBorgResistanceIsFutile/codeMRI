I propose a modern, **IDE-inspired "Workspace" Interface** designed for productivity and clarity. The goal is to separate the distinct phases of interaction (Management vs. Exploration) while keeping the AI assistant always accessible.

### **Design Philosophy: "The Technical Workbook"**
A clean, three-pane layout similar to VS Code or Obsidian, maximizing screen real estate for reading documentation and diagrams while keeping navigation and AI tools handy.

### **Core Layout Elements**

1.  **Global Top Bar**
    *   **Branding:** codeMRI Logo.
    *   **Context:** Currently active Repository (e.g., `/Users/levente/AI/nhibernate-core`).
    *   **Global Actions:** "New Ingest", "Refresh", "Settings".
    *   **Status Indicator:** A pulsating indicator for background tasks (Ingesting, Generating...).

2.  **Left Sidebar (Navigation & Structure)**
    *   **Tabs:**
        *   **Structure:** The generated Wiki hierarchy (collapsible tree view).
        *   **Files:** Raw file explorer (optional, for direct file lookup).
    *   **Filter:** Quick search input to filter the tree.

3.  **Center Stage (The Workspace)**
    *   **Dashboard View (Empty State):** Quick stats (Repo size, number of generated pages), and large "Start" buttons.
    *   **Document View:** The main reading area.
        *   **Markdown Rendering:** Typography optimized for technical reading.
        *   **Interactive Diagrams:** Mermaid.js diagrams rendered with pan/zoom capabilities.
        *   **Source Links:** Collapsible "Relevant Files" section linking to source code.

4.  **Right Sidebar (The Agent / Chat)**
    *   **Persistent:** Always visible (collapsible).
    *   **Context-Aware:** The agent sees the currently open document.
    *   **Streaming UI:** Typing indicators and streaming text for a responsive feel.
    *   **History:** Scrollable chat history.

---

### **Implementation Plan**

I will implement this using standard **Blazor** components and **Bootstrap 5** (with custom CSS overrides for a modern look), avoiding heavy third-party dependencies.

**Phase 1: Foundation & State**
1.  **`AppState.cs`**: A simplified state management service to hold the active repository, ingestion status, and wiki structure across components.
2.  **`MainLayout.razor`**: Refactor to support the 3-pane layout with CSS Grid.

**Phase 2: Components**
3.  **`IngestionControl.razor`**: A dedicated component for handling the ingestion process with visual feedback (spinners/progress bars).
4.  **`WikiTree.razor`**: A recursive component to render the `WikiStructure` hierarchy.
5.  **`ChatPanel.razor`**: A self-contained chat interface that sits in the right sidebar.

**Phase 3: Pages**
6.  **`Workspace.razor`**: The new main page that orchestrates these components.