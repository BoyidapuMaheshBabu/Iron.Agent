namespace Iron.Agent.Core;

public class ClickTool : ITool
{
    private readonly IDesktopAutomation _automation;

    public ClickTool(IDesktopAutomation automation)
    {
        _automation = automation;
    }

    public string Name => "click";

    public Task<bool> ExecuteAsync(Step step, CancellationToken cancellationToken = default)
    {
        var element = new UIElement(
            AutomationId: "",
            Name: step.Parameters.GetValueOrDefault("element_description", "").ToString(),
            ClassName: "",
            Value: null,
            IsEnabled: true,
            IsKeyboardFocusable: true,
            BoundingRect: new Rect(0, 0, 0, 0)
        );
        return _automation.ClickElementAsync(element, cancellationToken);
    }
}
