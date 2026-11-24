namespace Iron.Agent.App;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Iron.Agent.Core;

public partial class MainWindow : Window
{
    private readonly IOllamaClient _ollama;
    private readonly ITaskPlanner _planner;
    private readonly ITaskExecutor _executor;
    private readonly IPermissionService _permission;
    private readonly IEventPublisher _events;
    private readonly IAgentState _agentState;
    private readonly PetWindow _petWindow;
    private string _defaultModel = "llama3.2:3b";

    public MainWindow(IOllamaClient ollama, ITaskPlanner planner, ITaskExecutor executor,
        IPermissionService permission, IEventPublisher events, IAgentState agentState, PetWindow petWindow)
    {
        InitializeComponent();
        _ollama = ollama;
        _planner = planner;
        _executor = executor;
        _permission = permission;
        _permission.PermissionRequested += OnPermissionRequested;
        _events = events;
        _agentState = agentState;
        _petWindow = petWindow;

        _events.EventRaised += OnEventRaised;
        _agentState.StateChanged += OnAgentStateChanged;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _petWindow.Show();

        var modelsTask = _ollama.ListModelsAsync();
        modelsTask.Wait(TimeSpan.FromSeconds(5));
        if (modelsTask.IsCompletedSuccessfully && modelsTask.Result.Length > 0)
        {
            _defaultModel = modelsTask.Result[0];
        }

        AddMessage("Iron Agent is ready. How can I help you today?", isUser: false);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        this.Hide();
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            SendButton_Click(sender, null);
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        var text = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        InputBox.Clear();
        AddMessage(text, isUser: true);
        ShowTyping(true);

        try
        {
            if (text.StartsWith("/stop", StringComparison.OrdinalIgnoreCase))
            {
                _executor.Cancel();
                AddMessage("Task cancelled.", isUser: false);
                ShowTyping(false);
                return;
            }

            if (text.StartsWith("/pause", StringComparison.OrdinalIgnoreCase))
            {
                _executor.Pause();
                AddMessage("Task paused.", isUser: false);
                ShowTyping(false);
                return;
            }

            if (text.StartsWith("/resume", StringComparison.OrdinalIgnoreCase))
            {
                _executor.Resume();
                AddMessage("Task resumed.", isUser: false);
                ShowTyping(false);
                return;
            }

            var plan = await _planner.PlanAsync(text, _defaultModel);
            AddTaskToList(plan);

            await ExecutePlanWithStreaming(plan);
        }
        catch (Exception ex)
        {
            AddMessage($"Error: {ex.Message}", isUser: false);
        }
        finally
        {
            ShowTyping(false);
        }
    }

    private async Task ExecutePlanWithStreaming(TaskPlan plan)
    {
        var cts = new CancellationTokenSource();

        var responseBlock = AddStreamingMessage();

        var executionTask = _executor.ExecuteAsync(plan, cts.Token);
        var streamingTask = StreamResponse(plan.UserInstruction, responseBlock, cts.Token);

        await Task.WhenAll(executionTask, streamingTask);
    }

    private async Task StreamResponse(string instruction, TextBlock responseBlock, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var chunk in _ollama.GenerateStreamAsync(instruction, _defaultModel, cancellationToken))
            {

                Dispatcher.Invoke(() =>
                {
                    responseBlock.Text += chunk;
                    ChatScroll.ScrollToEnd();
                });
            }
        }
        catch (OperationCanceledException) { }
    }

    private TextBlock AddStreamingMessage()
    {
        var block = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(50, 2, 5, 2)
        };
        ChatMessages.Items.Add(block);
        ChatScroll.ScrollToEnd();
        return block;
    }

    private void AddMessage(string text, bool isUser)
    {
        var brush = isUser ? (Brush)new BrushConverter().ConvertFromString("#4cc9f0") : (Brush)new BrushConverter().ConvertFromString("#16213e");
        var margin = isUser ? new Thickness(5, 2, 50, 2) : new Thickness(50, 2, 5, 2);
        var radius = isUser ? new CornerRadius(12, 12, 0, 12) : new CornerRadius(12, 12, 12, 0);

        var container = new Border
        {
            Background = brush,
            CornerRadius = radius,
            Padding = new Thickness(10),
            Margin = margin,
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MaxWidth = 450,
            Child = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            }
        };

        ChatMessages.Items.Add(container);
        ChatScroll.ScrollToEnd();
    }

    private void AddTaskToList(TaskPlan plan)
    {
        Dispatcher.Invoke(() =>
        {
            var item = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString("#16213e"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8),
                Margin = new Thickness(2),
                BorderBrush = (Brush)new BrushConverter().ConvertFromString("#4cc9f0"),
                BorderThickness = new Thickness(0.5),
                Child = new TextBlock
                {
                    Text = $"? {plan.UserInstruction}",
                    Foreground = Brushes.White,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            TaskList.Items.Add(item);
        });
    }

    private void ShowTyping(bool show)
    {
        TypingIndicator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show) ChatScroll.ScrollToEnd();
    }

    private void OnEventRaised(object sender, AgentEvent ev)
    {
        Dispatcher.Invoke(() =>
        {
            switch (ev.Type)
            {
                case "task_completed":
                    AddMessage("Task completed successfully!", isUser: false);
                    break;
                case "task_cancelled":
                    AddMessage("Task was cancelled.", isUser: false);
                    break;
                case "task_error":
                case "step_error":
                    AddMessage(ev.Message, isUser: false);
                    break;
                case "permission_request":
                    AddMessage($"Permission needed: {ev.Message}", isUser: false);
                    break;
            }
        });
    }

    private void OnPermissionRequested(object sender, PermissionRequest request)
    {
        Dispatcher.Invoke(() =>
        {
            var result = MessageBox.Show(
                $"Permission needed for: {request.Description}\nTarget: {request.Target}\nAction: {request.ActionClass}",
                "Permission Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            _permission.CompletePermission(request.Id, result == MessageBoxResult.Yes);
        });
    }
    private void OnAgentStateChanged(object sender, AgentState state) { }

    private void NewChat_Click(object sender, RoutedEventArgs e)
    {
        ChatMessages.Items.Clear();
        AddMessage("New chat started. How can I help?", isUser: false);
    }

    public void ShowSettings() { this.Show(); this.WindowState = WindowState.Normal; this.Activate(); InputBox.Focus(); }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
        InputBox.Focus();
    }
}
