namespace Iron.Agent.Core;

public class ActivateWindowTool : ITool
{
    private readonly IDesktopAutomation _automation;

    public ActivateWindowTool(IDesktopAutomation automation)
    {
        _automation = automation;
    }

    public string Name => "activate_window";

    public Task<bool> ExecuteAsync(Step step, CancellationToken cancellationToken = default)
    {
        return _automation.ActivateWindowAsync(step.Parameters.GetValueOrDefault("window_title", "").ToString(), cancellationToken);
    }
}
