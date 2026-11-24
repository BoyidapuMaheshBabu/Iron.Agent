using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Iron.Agent.Core;

namespace Iron.Agent.App;

public partial class App : Application
{
    public IServiceProvider ServiceProvider { get; private set; }

    public App()
    {
        var services = new ServiceCollection();

        // Core services
        services.AddSingleton<IAgentState, AgentStateService>();
        services.AddSingleton<IEventPublisher, EventPublisher>();
        services.AddSingleton<IPermissionService, PermissionService>(sp => 
            new PermissionService(sp.GetRequiredService<IEventPublisher>()));
        services.AddSingleton<IConfiguration, Configuration>();

        // Ollama
        services.AddSingleton<IOllamaClient, OllamaClient>(sp => 
            new OllamaClient("http://localhost:11434"));

        // Automation
        services.AddSingleton<IDesktopAutomation, DesktopAutomation>();
        // Tools
        services.AddSingleton<ITool, ActivateWindowTool>();
        services.AddSingleton<ITool, ClickTool>();
        services.AddSingleton<ITool, TypeTool>();
        services.AddSingleton<ITool, PressKeyTool>();
        services.AddSingleton<ITool, StartProcessTool>();
        services.AddSingleton<ITool, StopProcessTool>();
        services.AddSingleton<ITool, ExecuteCommandTool>();
        services.AddSingleton<ITool, ReadFileTool>();
        services.AddSingleton<ITool, WriteFileTool>();

        // Agent
        services.AddSingleton<ITaskPlanner, TaskPlanner>(sp =>
            new TaskPlanner(sp.GetRequiredService<IOllamaClient>(), "llama3.2:3b"));
        services.AddSingleton<ITaskExecutor, TaskExecutor>(sp =>
            new TaskExecutor(
                sp.GetRequiredService<IEnumerable<ITool>>(),
                sp.GetRequiredService<IPermissionService>(),
                sp.GetRequiredService<IEventPublisher>(),
                sp.GetRequiredService<IAgentState>()));

        // UI
        services.AddSingleton<PetWindow>();
        services.AddSingleton<MainWindow>(sp =>
            new MainWindow(
                sp.GetRequiredService<IOllamaClient>(),
                sp.GetRequiredService<ITaskPlanner>(),
                sp.GetRequiredService<ITaskExecutor>(),
                sp.GetRequiredService<IPermissionService>(),
                sp.GetRequiredService<IEventPublisher>(),
                sp.GetRequiredService<IAgentState>(),
                sp.GetRequiredService<PetWindow>()));

        ServiceProvider = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}

