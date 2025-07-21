# Appendix B: UpdateHistory SQLite Schema

## B.1 Main Tables

### UpdateHistory Table
```sql
CREATE TABLE UpdateHistory (
    -- Primary Key
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    
    -- Resource identification (from BaseResourceProperties)
    ResourceId TEXT NOT NULL UNIQUE,
    ResourceName TEXT NOT NULL,
    ResourceType TEXT NOT NULL DEFAULT 'updateRuns',
    ParentId TEXT,
    
    -- UpdateRunClient fields
    TimeStarted TEXT, -- ISO 8601 format
    LastUpdatedTime TEXT, -- ISO 8601 format
    Duration INTEGER DEFAULT 0, -- Store as seconds
    State TEXT NOT NULL CHECK (State IN ('NotStarted', 'InProgress', 'Succeeded', 'Failed', 'Cancelled')),
    
    -- Progress tracking (denormalized from Step object)
    ProgressStatus TEXT,
    ProgressDescription TEXT,
    CurrentStepName TEXT,
    CurrentStepStatus TEXT,
    TotalSteps INTEGER DEFAULT 0,
    CompletedSteps INTEGER DEFAULT 0,
    ProgressXml TEXT, -- Serialized XML for progress details
    
    -- Additional fields for history tracking
    UpdateName TEXT,
    UpdateVersion TEXT,
    ActionPlanId TEXT,
    ActionPlanInstanceId TEXT,
    
    -- Tracking fields
    CreatedTime TEXT NOT NULL DEFAULT (datetime('now')),
    LastWriteTime TEXT NOT NULL DEFAULT (datetime('now')),
    Version INTEGER NOT NULL DEFAULT 1,
    
    -- Constraints
    CHECK (CompletedSteps <= TotalSteps),
    CHECK (Duration >= 0)
);
```

## B.2 History Detail Tables

### UpdateHistoryStep Table
```sql
-- Detailed step history table with recursive structure
CREATE TABLE UpdateHistoryStep (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UpdateHistoryId INTEGER NOT NULL,
    ParentStepId INTEGER, -- For recursive step hierarchy
    StepName TEXT NOT NULL,
    StepType TEXT,
    StepDescription TEXT,
    StepStatus TEXT CHECK (StepStatus IN ('NotStarted', 'InProgress', 'Succeeded', 'Failed', 'Skipped')),
    StartTime TEXT, -- ISO 8601 format
    EndTime TEXT, -- ISO 8601 format
    ExpectedDuration INTEGER DEFAULT 0, -- seconds
    ActualDuration INTEGER DEFAULT 0, -- seconds
    ErrorMessage TEXT,
    StepData TEXT, -- Serialized step information (XML/JSON)
    DisplayOrder INTEGER DEFAULT 0, -- Order within parent
    Depth INTEGER DEFAULT 0, -- Nesting depth (0 for root steps)
    
    FOREIGN KEY (UpdateHistoryId) REFERENCES UpdateHistory(Id) ON DELETE CASCADE,
    FOREIGN KEY (ParentStepId) REFERENCES UpdateHistoryStep(Id) ON DELETE CASCADE,
    CHECK (ActualDuration >= 0),
    CHECK (ExpectedDuration >= 0)
);
```

## B.3 Performance Indexes

```sql
CREATE INDEX IX_UpdateHistory_ResourceId ON UpdateHistory(ResourceId);
CREATE INDEX IX_UpdateHistory_TimeStarted ON UpdateHistory(TimeStarted);
CREATE INDEX IX_UpdateHistory_State ON UpdateHistory(State);
CREATE INDEX IX_UpdateHistory_UpdateName ON UpdateHistory(UpdateName);
CREATE INDEX IX_UpdateHistory_ActionPlanId ON UpdateHistory(ActionPlanId);
CREATE INDEX IX_UpdateHistory_CreatedTime ON UpdateHistory(CreatedTime);

-- Indexes for UpdateHistoryStep
CREATE INDEX IX_UpdateHistoryStep_UpdateHistoryId ON UpdateHistoryStep(UpdateHistoryId);
CREATE INDEX IX_UpdateHistoryStep_ParentStepId ON UpdateHistoryStep(ParentStepId);
CREATE INDEX IX_UpdateHistoryStep_StartTime ON UpdateHistoryStep(StartTime);
CREATE INDEX IX_UpdateHistoryStep_StepStatus ON UpdateHistoryStep(StepStatus);
CREATE INDEX IX_UpdateHistoryStep_DisplayOrder ON UpdateHistoryStep(DisplayOrder);
```

## B.4 Database Triggers

```sql
CREATE TRIGGER UpdateHistory_LastWriteTime
AFTER UPDATE ON UpdateHistory
BEGIN
    UPDATE UpdateHistory SET LastWriteTime = datetime('now'), Version = Version + 1
    WHERE Id = NEW.Id;
END;
```