namespace Iron.Agent.Core;

public class StopProcessTool : ITool
{
    private readonly IDesktopAutomation _automation;

    public StopProcessTool(IDesktopAutomation automation)
    {
        _automation = automation;
    }

    public string Name => "stop_process";

    public Task<bool> ExecuteAsync(Step step, CancellationToken cancellationToken = default)
    {
        return _automation.StopProcessAsync(
            step.Parameters.GetValueOrDefault("process_name", "").ToString(),
            cancellationToken);
    }
}
