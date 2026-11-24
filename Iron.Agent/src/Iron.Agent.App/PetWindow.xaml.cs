namespace Iron.Agent.App;

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Iron.Agent.Core;

public partial class PetWindow : Window
{
    private readonly IAgentState _agentState;
    private readonly IEventPublisher _events;
    private readonly MainWindow _mainWindow;
    private DispatcherTimer _animationTimer;
    private int _blinkCounter;
    private bool _isBlinking;

    public PetWindow(IAgentState agentState, IEventPublisher events, MainWindow mainWindow)
    {
        InitializeComponent();
        _agentState = agentState;
        _events = events;
        _mainWindow = mainWindow;

        _agentState.StateChanged += OnAgentStateChanged;
        _events.EventRaised += OnEventRaised;

        StartIdleAnimation();
    }

    private void StartIdleAnimation()
    {
        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    private void OnAnimationTick(object sender, EventArgs e)
    {
        if (_isBlinking) return;

        _blinkCounter++;
        if (_blinkCounter % 60 == 0) // Blink every ~6 seconds
        {
            Blink();
        }

        // Subtle idle bounce
        var offset = Math.Sin(_blinkCounter * 0.1) * 1;
        var transform = new TranslateTransform(0, offset);
        this.RenderTransform = transform;
    }

    private void Blink()
    {
        _isBlinking = true;
        LeftEye.Height = 2;
        RightEye.Height = 2;

        var restoreTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        restoreTimer.Tick += (s, args) =>
        {
            LeftEye.Height = 14;
            RightEye.Height = 14;
            _isBlinking = false;
            restoreTimer.Stop();
        };
        restoreTimer.Start();
    }

    private void OnAgentStateChanged(object sender, AgentState state)
    {
        Dispatcher.Invoke(() =>
        {
            UpdatePetVisuals(state);
        });
    }

    private void OnEventRaised(object sender, AgentEvent ev)
    {
        Dispatcher.Invoke(() =>
        {
            switch (ev.Type)
            {
                case "step_started":
                    ShowSpeech(ev.Message, 2000);
                    break;
                case "task_completed":
                    ShowSpeech("Task completed!", 3000);
                    break;
                case "task_cancelled":
                    ShowSpeech("Task cancelled", 2000);
                    break;
                case "task_error":
                case "step_error":
                    ShowSpeech("Error occurred", 3000);
                    break;
                case "permission_request":
                    ShowSpeech("Permission needed", 3000);
                    break;
            }
        });
    }

    private void UpdatePetVisuals(AgentState state)
    {
        var color = state switch
        {
            AgentState.Idle => "#4cc9f0",
            AgentState.Listening => "#f72585",
            AgentState.Thinking => "#f8961e",
            AgentState.Working => "#4cc9f0",
            AgentState.Success => "#2ecc71",
            AgentState.Error => "#e74c3c",
            _ => "#4cc9f0"
        };

        var brush = (Brush)new BrushConverter().ConvertFrom(color);
        StateIndicator.Fill = brush;

        var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)));
        StateIndicator.BeginAnimation(OpacityProperty, fadeIn);

        var mouthData = state switch
        {
            AgentState.Listening => "M 30,50 Q 45,60 60,50",
            AgentState.Thinking => "M 35,55 L 45,58 L 55,55",
            AgentState.Working => "M 30,55 Q 45,65 60,55",
            AgentState.Success => "M 30,50 Q 45,58 60,50",
            AgentState.Error => "M 30,58 Q 45,50 60,58",
            _ => "M 30,55 Q 45,65 60,55"
        };

        var mouthAnim = new StringAnimationUsingKeyFrames();
        mouthAnim.KeyFrames.Add(new DiscreteStringKeyFrame(mouthData, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        Mouth.BeginAnimation(Path.DataProperty, mouthAnim);
    }

    private void ShowSpeech(string text, int durationMs)
    {
        SpeechText.Text = text;
        SpeechBubble.Visibility = Visibility.Visible;
        SpeechBubble.Opacity = 0;

        var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)));
        SpeechBubble.BeginAnimation(OpacityProperty, fadeIn);

        var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
        hideTimer.Tick += (s, args) =>
        {
            var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(300)));
            fadeOut.Completed += (s2, args2) =>
            {
                SpeechBubble.Visibility = Visibility.Collapsed;
            };
            SpeechBubble.BeginAnimation(OpacityProperty, fadeOut);
            hideTimer.Stop();
        };
        hideTimer.Start();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Position at bottom-right of screen
        var workingArea = SystemParameters.WorkArea;
        this.Left = workingArea.Right - this.Width - 20;
        this.Top = workingArea.Bottom - this.Height - 20;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            this.DragMove();
    }

    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (this.ContextMenu != null)
            this.ContextMenu.IsOpen = true;
    }

    private void ShowMainWindow_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow.ShowSettings();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
