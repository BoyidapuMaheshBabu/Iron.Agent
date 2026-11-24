namespace Iron.Agent.Core;

public class TypeTool : ITool
{
    private readonly IDesktopAutomation _automation;

    public TypeTool(IDesktopAutomation automation)
    {
        _automation = automation;
    }

    public string Name => "type";

    public Task<bool> ExecuteAsync(Step step, CancellationToken cancellationToken = default)
    {
        var element = new UIElement(
            AutomationId: "",
            Name: "",
            ClassName: "",
            Value: null,
            IsEnabled: true,
            IsKeyboardFocusable: true,
            BoundingRect: new Rect(0, 0, 0, 0)
        );
        return _automation.TypeTextAsync(element, step.Parameters.GetValueOrDefault("text", "").ToString(), cancellationToken);
    }
}
