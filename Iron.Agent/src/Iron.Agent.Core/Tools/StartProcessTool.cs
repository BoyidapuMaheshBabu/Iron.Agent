namespace Iron.Agent.Core;

public class StartProcessTool : ITool
{
    private readonly IDesktopAutomation _automation;

    public StartProcessTool(IDesktopAutomation automation)
    {
        _automation = automation;
    }

    public string Name => "start_process";

    public Task<bool> ExecuteAsync(Step step, CancellationToken cancellationToken = default)
    {
        return _automation.StartProcessAsync(
            step.Parameters.GetValueOrDefault("file_name", "").ToString(),
            step.Parameters.GetValueOrDefault("arguments", "").ToString(),
            cancellationToken);
    }
}
