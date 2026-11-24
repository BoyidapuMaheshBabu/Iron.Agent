namespace Iron.Agent.Core;

public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Paused
}

public enum StepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped
}

public enum ActionClass
{
    Read,
    Write,
    Execute,
    System,
    Destructive
}

public enum AgentState
{
    Idle,
    Listening,
    Thinking,
    Working,
    Success,
    Error
}

public record TaskPlan(
    string Id,
    string UserInstruction,
    Step[] Steps,
    TaskStatus Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage
);

public record Step(
    int Order,
    string Tool,
    string Description,
    Dictionary<string, object> Parameters,
    string Verification,
    ActionClass ActionClass,
    StepStatus Status,
    string? ErrorMessage,
    DateTime? StartedAt,
    DateTime? CompletedAt
);

public record UIElement(
    string AutomationId,
    string Name,
    string ClassName,
    string? Value,
    bool IsEnabled,
    bool IsKeyboardFocusable,
    Rect BoundingRect
);

public record Rect(int Left, int Top, int Right, int Bottom);

public record PermissionRequest(
    Guid Id,
    ActionClass ActionClass,
    string Target,
    string Description,
    string StepDescription,
    DateTime CreatedAt
);

public record AgentEvent(
    string Type,
    string Message,
    string? TaskId,
    int? StepOrder,
    DateTime Timestamp,
    Dictionary<string, object>? Metadata
);

public record AutomationResult(
    bool Success,
    string? ErrorMessage,
    object? Data
);