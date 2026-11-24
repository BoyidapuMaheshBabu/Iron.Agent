namespace Iron.Agent.Core;

public class PressKeyTool : ITool
{
    private readonly IDesktopAutomation _automation;

    public PressKeyTool(IDesktopAutomation automation)
    {
        _automation = automation;
    }

    public string Name => "press_key";

    public Task<bool> ExecuteAsync(Step step, CancellationToken cancellationToken = default)
    {
        var key = step.Parameters.GetValueOrDefault("key", "ENTER").ToString() ?? "ENTER";
        return _automation.PressKeyAsync(key, cancellationToken);
    }
}
