namespace Iron.Agent.Tests;

using Iron.Agent.Core;
using Xunit;

public class TaskExecutorTests
{
    private class StubDesktopAutomation : IDesktopAutomation
    {
        public Task<UIElement[]> FindElementsAsync(string windowTitle, string automationId = null, string className = null, CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<UIElement>());
        public Task<bool> ClickElementAsync(UIElement element, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> TypeTextAsync(UIElement element, string text, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> PressKeyAsync(string keyName, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> ActivateWindowAsync(string windowTitle, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string[]> ListRunningProcessesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<string>());
        public Task<bool> StartProcessAsync(string fileName, string arguments, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> StopProcessAsync(string processName, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<AutomationResult> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default) => Task.FromResult(new AutomationResult(true, null, null));
    }

    private class StubPermissionService : IPermissionService
    {
        public event EventHandler<PermissionRequest> PermissionRequested;
        public Task<bool> RequestPermissionAsync(PermissionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public bool IsAllowed(ActionClass actionClass, string target) => true;
        public void AddAllowlist(ActionClass actionClass, string target) { }
        public void CompletePermission(Guid requestId, bool granted) { }
    }

    private class StubEventPublisher : IEventPublisher
    {
        public event EventHandler<AgentEvent> EventRaised;
        public readonly List<AgentEvent> Events = new();
        public void Publish(AgentEvent ev) => Events.Add(ev);
    }

    private class StubAgentState : IAgentState
    {
        public AgentState Current { get; private set; }
        public event EventHandler<AgentState> StateChanged;
        public void SetState(AgentState state) { Current = state; StateChanged?.Invoke(this, state); }
    }

    private class StubTool : ITool
    {
        public string Name => "read_file";
        public Task<bool> ExecuteAsync(Step step, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    [Fact]
    public async Task ExecuteAsync_RunsSimplePlan_AndCompletes()
    {
        var state = new StubAgentState();
        var events = new StubEventPublisher();
        var executor = new TaskExecutor(
            new[] { new StubTool() },
            new StubPermissionService(),
            events,
            state);

        var plan = new TaskPlan(
            Id: "test-plan-1",
            UserInstruction: "read file",
            Steps: new[]
            {
                new Step(
                    Order: 1,
                    Tool: "read_file",
                    Description: "Read the file",
                    Parameters: new Dictionary<string, object> { { "path", "test.txt" } },
                    Verification: "File read",
                    ActionClass: ActionClass.Read,
                    Status: StepStatus.Pending,
                    ErrorMessage: null,
                    StartedAt: null,
                    CompletedAt: null)
            },
            Status: TaskStatus.Pending,
            CreatedAt: DateTime.UtcNow,
            StartedAt: null,
            CompletedAt: null,
            ErrorMessage: null);

        await executor.ExecuteAsync(plan);

        Assert.Equal(AgentState.Success, state.Current);
        Assert.Contains(events.Events, e => e.Type == "task_completed");
    }
}
