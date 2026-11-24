namespace Iron.Agent.Core;

using System.Text.Json;

public class TaskPlanner : ITaskPlanner
{
    private readonly IOllamaClient _ollama;
    private readonly string _defaultModel;

    public TaskPlanner(IOllamaClient ollama, string defaultModel = "llama3.2:3b")
    {
        _ollama = ollama;
        _defaultModel = defaultModel;
    }

    public async Task<TaskPlan> PlanAsync(string instruction, string model = null, CancellationToken cancellationToken = default)
    {
        var modelName = model ?? _defaultModel;
        var systemPrompt = @"You are a task planning AI for a desktop automation agent. Break down the user's instruction into a sequence of executable steps.

Available tools:
- activate_window: Bring an application window to foreground (params: window_title)
- click: Click a UI element (params: element_description)
- type: Type text into focused element (params: text)
- press_key: Press a keyboard key (params: key)
- start_process: Launch an application (params: file_name, arguments)
- stop_process: Stop a running process (params: process_name)
- execute_command: Run a terminal command (params: command)
- read_file: Read a text file (params: path)
- write_file: Write content to a file (params: path, content)

Action classes:
- Read: reading files, observing state
- Write: creating/modifying files
- Execute: running commands, launching apps
- System: window management, process control
- Destructive: deleting files, killing processes

Respond with ONLY valid JSON matching this schema:
{
  ""steps"": [
    {
      ""order"": 1,
      ""tool"": ""tool_name"",
      ""description"": ""human readable description"",
      ""parameters"": { ""key"": ""value"" },
      ""verification"": ""how to verify this step succeeded"",
      ""action_class"": ""Read|Write|Execute|System|Destructive""
    }
  ]
}

Be concise. Each step should be atomic and verifiable. Include 1-5 steps max for simple instructions.";

        var userMessage = $"Instruction: {instruction}";

        try
        {
            var response = await _ollama.ChatAsync(systemPrompt, userMessage, modelName, cancellationToken);
            var plan = ParsePlan(response, instruction);
            return plan;
        }
        catch (Exception ex)
        {
            throw new PlanningException($"Failed to plan task: {ex.Message}", ex);
        }
    }

    private TaskPlan ParsePlan(string response, string instruction)
    {
        try
        {
            // Extract JSON from response (handle markdown code blocks)
            var json = ExtractJson(response);
            var doc = JsonDocument.Parse(json);
            var stepsArray = doc.RootElement.GetProperty("steps");

            var steps = new List<Step>();
            int order = 1;

            foreach (var stepElem in stepsArray.EnumerateArray())
            {
                var tool = stepElem.GetProperty("tool").GetString();
                var description = stepElem.GetProperty("description").GetString();
                var verification = stepElem.GetProperty("verification").GetString();
                var actionClassStr = stepElem.GetProperty("action_class").GetString();
                var actionClass = Enum.Parse<ActionClass>(actionClassStr);

                var parameters = new Dictionary<string, object>();
                if (stepElem.TryGetProperty("parameters", out var paramsElem))
                {
                    foreach (var prop in paramsElem.EnumerateObject())
                    {
                        parameters[prop.Name] = prop.Value.GetRawText().Trim('"');
                    }
                }

                steps.Add(new Step(
                    Order: order++,
                    Tool: tool,
                    Description: description,
                    Parameters: parameters,
                    Verification: verification,
                    ActionClass: actionClass,
                    Status: StepStatus.Pending,
                    ErrorMessage: null,
                    StartedAt: null,
                    CompletedAt: null
                ));
            }

            return new TaskPlan(
                Id: Guid.NewGuid().ToString("N")[..12],
                UserInstruction: instruction,
                Steps: steps.ToArray(),
                Status: TaskStatus.Pending,
                CreatedAt: DateTime.UtcNow,
                StartedAt: null,
                CompletedAt: null,
                ErrorMessage: null
            );
        }
        catch (Exception ex)
        {
            // Fallback: single step plan
            return new TaskPlan(
                Id: Guid.NewGuid().ToString("N")[..12],
                UserInstruction: instruction,
                Steps: new[]
                {
                    new Step(
                        Order: 1,
                        Tool: "execute_command",
                        Description: instruction,
                        Parameters: new Dictionary<string, object> { { "command", instruction } },
                        Verification: "Command executed without error",
                        ActionClass: ActionClass.Execute,
                        Status: StepStatus.Pending,
                        ErrorMessage: null,
                        StartedAt: null,
                        CompletedAt: null
                    )
                },
                Status: TaskStatus.Pending,
                CreatedAt: DateTime.UtcNow,
                StartedAt: null,
                CompletedAt: null,
                ErrorMessage: null
            );
        }
    }

    private string ExtractJson(string response)
    {
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        if (start >= 0 && end > start)
            return response[start..(end + 1)];
        return response;
    }
}

public class TaskExecutor : ITaskExecutor
{
    private readonly Dictionary<string, ITool> _tools;
    private readonly IPermissionService _permission;
    private readonly IEventPublisher _events;
    private readonly IAgentState _state;

    private CancellationTokenSource _cts;
    private Task _currentTask;
    private TaskPlan _currentPlan;
    private bool _paused;
    private readonly object _lock = new();

    public TaskExecutor(IEnumerable<ITool> tools, IPermissionService permission, IEventPublisher events, IAgentState state)
    {
        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        _permission = permission;
        _events = events;
        _state = state;
    }

    public async Task ExecuteAsync(TaskPlan plan, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_currentTask != null && !_currentTask.IsCompleted)
                throw new InvalidOperationException("A task is already running");

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _currentPlan = plan;
            _paused = false;
        }

        _currentTask = ExecutePlanAsync(plan, _cts.Token);
        await _currentTask;
    }

    public void Cancel()
    {
        lock (_lock)
        {
            _cts?.Cancel();
        }
    }

    public void Pause()
    {
        lock (_lock) { _paused = true; }
    }

    public void Resume()
    {
        lock (_lock) { _paused = false; }
    }

    public TaskStatus GetStatus(string taskId)
    {
        if (_currentPlan?.Id != taskId) return TaskStatus.Pending;
        return _currentPlan.Status;
    }

    private async Task ExecutePlanAsync(TaskPlan plan, CancellationToken cancellationToken)
    {
        _state.SetState(AgentState.Working);
        var completedSteps = new List<Step>();

        try
        {
            foreach (var step in plan.Steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                while (_paused)
                {
                    await Task.Delay(100, cancellationToken);
                }

                var stepResult = await ExecuteStepAsync(step, cancellationToken);
                completedSteps.Add(stepResult);

                if (stepResult.Status == StepStatus.Failed)
                {
                    _events.Publish(new AgentEvent("task_failed", $"Step {step.Order} failed: {stepResult.ErrorMessage}", plan.Id, step.Order, DateTime.UtcNow, null));
                    _state.SetState(AgentState.Error);
                    return;
                }
            }

            _events.Publish(new AgentEvent("task_completed", "Task completed successfully", plan.Id, null, DateTime.UtcNow, null));
            _state.SetState(AgentState.Success);
        }
        catch (OperationCanceledException)
        {
            _events.Publish(new AgentEvent("task_cancelled", "Task was cancelled", plan.Id, null, DateTime.UtcNow, null));
            _state.SetState(AgentState.Idle);
        }
        catch (Exception ex)
        {
            _events.Publish(new AgentEvent("task_error", $"Task error: {ex.Message}", plan.Id, null, DateTime.UtcNow, null));
            _state.SetState(AgentState.Error);
        }
    }

    private async Task<Step> ExecuteStepAsync(Step step, CancellationToken cancellationToken)
    {
        var startedStep = step with
        {
            Status = StepStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        _events.Publish(new AgentEvent("step_started", startedStep.Description, _currentPlan.Id, startedStep.Order, DateTime.UtcNow, null));

        try
        {
            // Check permission for destructive/system actions
            if (step.ActionClass == ActionClass.Destructive || step.ActionClass == ActionClass.System)
            {
                if (!_permission.IsAllowed(step.ActionClass, step.Parameters.Values.FirstOrDefault()?.ToString() ?? ""))
                {
                    var granted = await _permission.RequestPermissionAsync(new PermissionRequest(
                        Id: Guid.NewGuid(),
                        ActionClass: step.ActionClass,
                        Target: step.Parameters.Values.FirstOrDefault()?.ToString() ?? "unknown",
                        Description: step.Description,
                        StepDescription: step.Description,
                        CreatedAt: DateTime.UtcNow
                    ), cancellationToken);

                    if (!granted)
                    {
                        return startedStep with
                        {
                            Status = StepStatus.Skipped,
                            ErrorMessage = "Permission denied by user",
                            CompletedAt = DateTime.UtcNow
                        };
                    }

                    _permission.AddAllowlist(step.ActionClass, step.Parameters.Values.FirstOrDefault()?.ToString() ?? "");
                }
            }

            
            if (!_tools.TryGetValue(step.Tool, out var tool))
            {
                return startedStep with { Status = StepStatus.Failed, ErrorMessage = $"Unknown tool: {step.Tool}", CompletedAt = DateTime.UtcNow };
            }
            var success = await tool.ExecuteAsync(step, cancellationToken);


            var completedStep = startedStep with
            {
                Status = success ? StepStatus.Completed : StepStatus.Failed,
                ErrorMessage = success ? null : $"Step {step.Tool} failed",
                CompletedAt = DateTime.UtcNow
            };

            _events.Publish(new AgentEvent(
                success ? "step_completed" : "step_failed",
                startedStep.Description,
                _currentPlan.Id,
                startedStep.Order,
                DateTime.UtcNow,
                null));

            return completedStep;
        }
        catch (Exception ex)
        {
            var failedStep = startedStep with
            {
                Status = StepStatus.Failed,
                ErrorMessage = ex.Message,
                CompletedAt = DateTime.UtcNow
            };

            _events.Publish(new AgentEvent("step_error", $"Step error: {ex.Message}", _currentPlan.Id, startedStep.Order, DateTime.UtcNow, null));
            return failedStep;
        }
    }
}

public class PermissionService : IPermissionService
{
    private readonly HashSet<(ActionClass, string)> _allowlist = new();
    private readonly IEventPublisher _events;
    private readonly Dictionary<Guid, TaskCompletionSource<bool>> _pendingRequests = new();
    private readonly object _lock = new();

    public event EventHandler<PermissionRequest> PermissionRequested;

    public PermissionService(IEventPublisher events)
    {
        _events = events;
    }

    public Task<bool> RequestPermissionAsync(PermissionRequest request, CancellationToken cancellationToken = default)
    {
        _events.Publish(new AgentEvent(
            "permission_request",
            $"Permission requested: {request.Description}",
            null,
            null,
            DateTime.UtcNow,
            new Dictionary<string, object> { { "request", request } }));

        if (PermissionRequested == null)
        {
            return Task.FromResult(false);
        }

        var tcs = new TaskCompletionSource<bool>();
        lock (_lock)
        {
            _pendingRequests[request.Id] = tcs;
        }

        cancellationToken.Register(() => tcs.TrySetCanceled());

        PermissionRequested?.Invoke(this, request);
        return tcs.Task;
    }

    public bool IsAllowed(ActionClass actionClass, string target)
    {
        return _allowlist.Contains((actionClass, target ?? ""));
    }

    public void AddAllowlist(ActionClass actionClass, string target)
    {
        _allowlist.Add((actionClass, target ?? ""));
    }

    public void CompletePermission(Guid requestId, bool granted)
    {
        lock (_lock)
        {
            if (_pendingRequests.TryGetValue(requestId, out var tcs))
            {
                _pendingRequests.Remove(requestId);
                tcs.TrySetResult(granted);
            }
        }
    }
}

public class EventPublisher : IEventPublisher
{
    public event EventHandler<AgentEvent> EventRaised;

    public void Publish(AgentEvent ev)
    {
        EventRaised?.Invoke(this, ev);
    }
}

public class AgentStateService : IAgentState
{
    private AgentState _current = AgentState.Idle;
    public event EventHandler<AgentState> StateChanged;

    public AgentState Current => _current;

    public void SetState(AgentState state)
    {
        if (_current != state)
        {
            _current = state;
            StateChanged?.Invoke(this, state);
        }
    }
}

public class Configuration : IConfiguration
{
    private readonly Dictionary<string, object> _values = new();

    public T Get<T>(string key)
    {
        if (_values.TryGetValue(key, out var val))
            return (T)val;
        return default;
    }

    public void Set(string key, object value)
    {
        _values[key] = value;
    }

    public bool Contains(string key)
    {
        return _values.ContainsKey(key);
    }
}

