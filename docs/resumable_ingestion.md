# Resumable Change-Aware Ingestion Process

## Overview
This document describes the enhanced ingestion process that provides:

- **Resumability**: Ingestion can be interrupted and resumed from last checkpoint
- **Idempotency**: No duplicate ingestion of unchanged content
- **Incremental Processing**: Only processes changed content on subsequent runs
- **Checkpoint-based Recovery**: State saved after each batch for reliable recovery
- **State Persistence**: Processing state stored in SQLite database

## Architecture Components

### 1. State Tracking
The system tracks comprehensive state information to enable resumability:

- **File hashes**: MD5 hashes stored in ingestion manifest for change detection
- **Processing state**: Tracks queue, completed files, failed files, and progress
- **Checkpoints**: State saved after each batch for recovery
- **Status tracking**: Current status (idle, processing, paused, completed, error)

### 2. Database Schema
**New Table: `IngestionProcessingStates`**
```sql
CREATE TABLE IF NOT EXISTS IngestionProcessingStates (
    RepoId INTEGER PRIMARY KEY,
    JsonContent TEXT NOT NULL,  -- Serialized IngestionProcessingState object
    FOREIGN KEY(RepoId) REFERENCES Repositories(Id) ON DELETE CASCADE
);
```

**Enhanced `IngestionManifests` Table**
```sql
CREATE TABLE IF NOT EXISTS IngestionManifests (
    RepoId INTEGER PRIMARY KEY,
    JsonContent TEXT NOT NULL,  -- Dictionary of file paths and their hashes
    FOREIGN KEY(RepoId) REFERENCES Repositories(Id) ON DELETE CASCADE
);
```

### 3. Processing State Model
The `IngestionProcessingState` class tracks:

```csharp
public class IngestionProcessingState
{
    public List<string> ProcessingQueue { get; set; } = new(); // Files to process
    public List<string> CompletedFiles { get; set; } = new();  // Successfully processed
    public List<string> FailedFiles { get; set; } = new();     // Failed processing
    public int CurrentBatchSize { get; set; } = 50;            // Configurable batch size
    public DateTime LastCheckpoint { get; set; } = DateTime.UtcNow; // Last state save
    public string CurrentStatus { get; set; } = "idle";        // Current status
    public int CurrentPosition { get; set; } = 0;              // Position in queue
    public int TotalFiles { get; set; } = 0;                   // Total files to process
    public DateTime? StartedAt { get; set; }                   // When processing started
    public DateTime? CompletedAt { get; set; }                 // When processing completed
    public Dictionary<string, string> Errors { get; set; } = new(); // Error messages
}
```

## Workflow

### 1. Initialization
1. Load existing processing state (if any)
2. Check if existing process is stale (no checkpoint for >5 minutes)
3. Scan repository for files
4. Compare with stored hashes to identify new/changed files
5. Build processing queue
6. Initialize processing state

### 2. Processing
1. Process files in configurable batches
2. For each file:
   - Read content and calculate MD5 hash
   - Delete old chunks if file was previously ingested
   - Split into chunks and generate embeddings
   - Update manifest with new hash
3. Save checkpoint after each batch
4. Update processing state
5. Repeat until queue is empty

### 3. Recovery
1. On restart, load last known state
2. Check if process can be resumed (status = paused/error or stale processing)
3. Resume from last checkpoint
4. Skip already processed files
5. Continue processing remaining queue

## API Endpoints

### `POST /api/ingest`
**Request**: `IngestRequest`
```csharp
public class IngestRequest
{
    public string RepoPath { get; set; } = string.Empty;
    public bool Force { get; set; } = false;
    public bool Delete { get; set; } = false;
}
```

**Enhanced Functionality**:
- Checks for existing processing state
- Rejects new ingestion if active process exists (unless stale)
- Supports `Force` parameter to re-ingest all files
- Processes files in batches with checkpointing
- Tracks progress and errors
- Cleans up processing state on `Delete` requests

### `GET /api/ingest/status?repoPath={path}`
**Returns**: `IngestionStatusResponse`
```csharp
public class IngestionStatusResponse
{
    public string Status { get; set; } = "idle";          // Current status
    public int TotalFiles { get; set; }                   // Total files to process
    public int ProcessedFiles { get; set; }               // Files processed
    public int RemainingFiles { get; set; }               // Files remaining
    public int FailedFiles { get; set; }                  // Failed files
    public double ProgressPercentage { get; set; }        // Progress (0-100)
    public DateTime? StartedAt { get; set; }              // When started
    public DateTime? CompletedAt { get; set; }            // When completed
    public DateTime? LastCheckpoint { get; set; }         // Last checkpoint
    public List<string> RecentErrors { get; set; } = new(); // Recent errors
    public bool CanResume { get; set; }                   // Can this be resumed?
}
```

### `POST /api/ingest/resume`
**Request**: `ResumeIngestionRequest`
```csharp
public class ResumeIngestionRequest
{
    public string RepoPath { get; set; } = string.Empty;  // Repository path
    public int? BatchSize { get; set; }                   // Optional batch size
}
```

**Functionality**:
- Resumes interrupted ingestion
- Validates that process can be resumed
- Continues from last checkpoint
- Processes remaining queue
- Supports batch size override

## Error Handling

- **Transient errors**: Retry failed files, continue processing
- **Fatal errors**: Mark process as error state, save state for recovery
- **State consistency**: Processing state always saved before critical operations
- **Timeout detection**: Stale processes (>5 minutes without checkpoint) can be resumed
- **Error tracking**: Detailed error messages stored in processing state

## Implementation Details

### Change Detection
- MD5 hashes calculated for each file
- Hashes stored in ingestion manifest
- Files with changed hashes are re-ingested
- New files are always ingested

### Idempotency
- Files with unchanged hashes are skipped
- Old chunks are deleted before re-ingesting changed files
- Processing state prevents duplicate processing

### Resumability
- Checkpointing after each batch
- Processing state persistence
- Stale process detection
- Recovery from interruptions

## Testing Strategy

The system includes comprehensive tests for:

- **Resumability**: Interrupt and resume ingestion
- **Idempotency**: Multiple runs with same content
- **Change detection**: Modify files and verify re-ingestion
- **Error handling**: Simulate failures and verify recovery
- **Performance**: Large repositories with batch processing

## Usage Examples

### Starting a new ingestion
```bash
curl -X POST "http://localhost:5000/api/ingest" \
     -H "Content-Type: application/json" \
     -d '{"RepoPath": "/path/to/repository"}'
```

### Checking ingestion status
```bash
curl "http://localhost:5000/api/ingest/status?repoPath=/path/to/repository"
```

### Resuming interrupted ingestion
```bash
curl -X POST "http://localhost:5000/api/ingest/resume" \
     -H "Content-Type: application/json" \
     -d '{"RepoPath": "/path/to/repository"}'
```

### Forcing complete re-ingestion
```bash
curl -X POST "http://localhost:5000/api/ingest" \
     -H "Content-Type: application/json" \
     -d '{"RepoPath": "/path/to/repository", "Force": true}'
