namespace Iron.Agent.Core;

public class ExecuteCommandTool : ITool
{
    private readonly IDesktopAutomation _automation;

    public ExecuteCommandTool(IDesktopAutomation automation)
    {
        _automation = automation;
    }

    public string Name => "execute_command";

    public Task<bool> ExecuteAsync(Step step, CancellationToken cancellationToken = default)
    {
        return _automation.ExecuteCommandAsync(
            step.Parameters.GetValueOrDefault("command", "").ToString(),
            cancellationToken).ContinueWith(t => t.Result.Success, cancellationToken);
    }
}
