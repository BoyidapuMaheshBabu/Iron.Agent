namespace Iron.Agent.Core;

public interface IOllamaClient
{
    Task<string> GenerateAsync(string prompt, string model, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> GenerateStreamAsync(string prompt, string model, CancellationToken cancellationToken = default);
    Task<string> ChatAsync(string systemPrompt, string userMessage, string model, CancellationToken cancellationToken = default);
    Task<bool> IsModelAvailableAsync(string model, CancellationToken cancellationToken = default);
    Task<string[]> ListModelsAsync(CancellationToken cancellationToken = default);
}

public interface IDesktopAutomation
{
    Task<UIElement[]> FindElementsAsync(string windowTitle, string automationId = null, string className = null, CancellationToken cancellationToken = default);
    Task<bool> ClickElementAsync(UIElement element, CancellationToken cancellationToken = default);
    Task<bool> TypeTextAsync(UIElement element, string text, CancellationToken cancellationToken = default);
    Task<bool> PressKeyAsync(string keyName, CancellationToken cancellationToken = default);
    Task<bool> ActivateWindowAsync(string windowTitle, CancellationToken cancellationToken = default);
    Task<string[]> ListRunningProcessesAsync(CancellationToken cancellationToken = default);
    Task<bool> StartProcessAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
    Task<bool> StopProcessAsync(string processName, CancellationToken cancellationToken = default);
    Task<AutomationResult> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default);
}

public interface ITaskPlanner
{
    Task<TaskPlan> PlanAsync(string instruction, string model = null, CancellationToken cancellationToken = default);
}

public interface ITaskExecutor
{
    Task ExecuteAsync(TaskPlan plan, CancellationToken cancellationToken = default);
    void Cancel();
    void Pause();
    void Resume();
    TaskStatus GetStatus(string taskId);
}

public interface IPermissionService
{
    event EventHandler<PermissionRequest> PermissionRequested;
    Task<bool> RequestPermissionAsync(PermissionRequest request, CancellationToken cancellationToken = default);
    bool IsAllowed(ActionClass actionClass, string target);
    void AddAllowlist(ActionClass actionClass, string target);
    void CompletePermission(Guid requestId, bool granted);
}

public interface IEventPublisher
{
    event EventHandler<AgentEvent> EventRaised;
    void Publish(AgentEvent ev);
}

public interface IAgentState
{
    AgentState Current { get; }
    event EventHandler<AgentState> StateChanged;
    void SetState(AgentState state);
}

public interface IConfiguration
{
    T Get<T>(string key);
    void Set(string key, object value);
    bool Contains(string key);
}
