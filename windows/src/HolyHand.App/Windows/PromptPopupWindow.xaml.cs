using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Models;

namespace HolyHand.App.Windows;

public partial class PromptPopupWindow : Window
{
    private AppTarget? _currentTarget;
    private bool _isListening;
    private CancellationTokenSource? _speechCts;
    private DispatcherTimer? _resetStatusTimer;

    public bool IsExpanded { get; private set; } = true;

    public event Action<string, AppTarget?>? TaskSubmitted;
    public event Action? Cancelled;

    public ISpeechInput? SpeechInput { get; set; }

    public PromptPopupWindow()
    {
        InitializeComponent();
        _resetStatusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _resetStatusTimer.Tick += (s, e) =>
        {
            _resetStatusTimer.Stop();
            if (CollapsedStatusPillText != null)
            {
                CollapsedStatusPillText.Text = "Alt+Space";
                CollapsedStatusPillText.Foreground = new SolidColorBrush(Color.FromRgb(0x81, 0x8C, 0xF8));
            }
            if (StatusText != null)
            {
                StatusText.Text = "Ready • Enter ↵ to run • Esc to collapse • Alt+Space to toggle";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
            }
        };
    }

    public void Expand(AppTarget? target = null)
    {
        if (target != null)
        {
            _currentTarget = target;
            ApplyTargetInfo(target);
        }

        IsExpanded = true;
        CollapsedIsland.Visibility = Visibility.Collapsed;
        ExpandedIsland.Visibility = Visibility.Visible;

        Width = 640;
        Height = 140;
        Left = Math.Max(10, (SystemParameters.PrimaryScreenWidth - Width) / 2);
        Top = 28;

        WindowState = WindowState.Normal;
        Visibility = Visibility.Visible;
        Show();
        Activate();
        Topmost = true;
        Focus();
        PromptInput.Focus();
        Keyboard.Focus(PromptInput);

        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        if (helper.Handle != IntPtr.Zero)
        {
            global::Windows.Win32.PInvoke.SetForegroundWindow((global::Windows.Win32.Foundation.HWND)helper.Handle);
        }
    }

    public void Collapse()
    {
        _speechCts?.Cancel();
        IsExpanded = false;

        ExpandedIsland.Visibility = Visibility.Collapsed;
        CollapsedIsland.Visibility = Visibility.Visible;

        Width = 240;
        Height = 44;
        Left = Math.Max(10, (SystemParameters.PrimaryScreenWidth - Width) / 2);
        Top = 20;

        WindowState = WindowState.Normal;
        Visibility = Visibility.Visible;
        Show();
        Topmost = true;
    }

    public void Toggle(AppTarget? target = null)
    {
        if (IsExpanded)
        {
            Collapse();
        }
        else
        {
            Expand(target);
        }
    }

    public void ShowForTarget(AppTarget? target)
    {
        _currentTarget = target;
        _isListening = false;
        MicButton.Content = "🎤 Mic";

        ApplyTargetInfo(target);
        PromptInput.Text = string.Empty;
        Expand(target);
    }

    private void ApplyTargetInfo(AppTarget? target)
    {
        if (target != null)
        {
            var title = !string.IsNullOrWhiteSpace(target.WindowTitle) ? target.WindowTitle : target.ProcessName;
            TargetAppText.Text = $"Target: {target.ProcessName} — \"{title}\"";
            ElevatedBadge.Visibility = target.IsElevated ? Visibility.Visible : Visibility.Collapsed;

            if (target.IsElevated)
            {
                StatusText.Text = "⚠ Target process is elevated (Admin). UIPI restricts automation.";
                StatusText.Foreground = Brushes.OrangeRed;
            }
            else
            {
                StatusText.Text = "Ready • Enter ↵ to run • Esc to collapse • Alt+Space to toggle";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
            }
        }
        else
        {
            TargetAppText.Text = "Target: Windows Desktop";
            ElevatedBadge.Visibility = Visibility.Collapsed;
            StatusText.Text = "Ready • Enter ↵ to run • Esc to collapse • Alt+Space to toggle";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
        }
    }

    public void UpdateTarget(AppTarget target)
    {
        _currentTarget = target;
        Dispatcher.Invoke(() => ApplyTargetInfo(target));
    }

    public void SetExecuting(bool isExecuting, string? status = null)
    {
        Dispatcher.Invoke(() =>
        {
            PromptInput.IsEnabled = !isExecuting;
            SendButton.IsEnabled = !isExecuting;
            SendButton.Content = isExecuting ? "Running…" : "Run ↵";

            var activeDotBrush = isExecuting
                ? new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8))
                : new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));

            CollapsedDot.Fill = activeDotBrush;
            ExpandedDot.Fill = activeDotBrush;

            if (isExecuting)
            {
                _resetStatusTimer?.Stop();
                CollapsedStatusPillText.Text = "Running…";
                CollapsedStatusPillText.Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));

                if (!string.IsNullOrEmpty(status))
                {
                    StatusText.Text = status;
                    StatusText.Foreground = Brushes.LightSkyBlue;
                }
            }
            else
            {
                CollapsedStatusPillText.Text = "Alt+Space";
                CollapsedStatusPillText.Foreground = new SolidColorBrush(Color.FromRgb(0x81, 0x8C, 0xF8));
            }
        });
    }

    public void UpdateStatus(string status, bool isError = false)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = status;
            StatusText.Foreground = isError
                ? Brushes.OrangeRed
                : Brushes.LightSkyBlue;
        });
    }

    public void OnRunCompleted(string message, bool success)
    {
        Dispatcher.Invoke(() =>
        {
            SetExecuting(false);
            if (success)
            {
                StatusText.Text = "✓ " + message;
                StatusText.Foreground = Brushes.LimeGreen;

                CollapsedStatusPillText.Text = "✓ Done";
                CollapsedStatusPillText.Foreground = Brushes.LimeGreen;
                CollapsedDot.Fill = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
            }
            else
            {
                string friendlyMessage = message;
                if (friendlyMessage.Contains("below threshold", StringComparison.OrdinalIgnoreCase))
                {
                    var targetName = _currentTarget != null && !string.IsNullOrWhiteSpace(_currentTarget.ProcessName)
                        ? _currentTarget.ProcessName
                        : "the active window";
                    friendlyMessage = $"No matching action found in {targetName}. Try specifying an exact action, app, or system command.";
                }
                StatusText.Text = "⚠ " + friendlyMessage;
                StatusText.Foreground = Brushes.OrangeRed;

                CollapsedStatusPillText.Text = "⚠ Alert";
                CollapsedStatusPillText.Foreground = Brushes.OrangeRed;
            }

            _resetStatusTimer?.Stop();
            _resetStatusTimer?.Start();

            // Settle onto screen
            Show();
            Topmost = true;
        });
    }

    public void HidePopup()
    {
        _speechCts?.Cancel();
        SetExecuting(false);
        Collapse();
        Cancelled?.Invoke();
    }

    private void Submit()
    {
        _speechCts?.Cancel();
        var goal = PromptInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(goal)) return;

        if (_currentTarget?.IsElevated == true)
        {
            MessageBox.Show(
                "HolyHand cannot automate an elevated (Administrator) application.\n\nPlease activate a standard window or restart HolyHand as administrator.",
                "Target is Elevated",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Set running state and notify loop
        SetExecuting(true, $"⚡ Processing \"{goal}\"…");
        TaskSubmitted?.Invoke(goal, _currentTarget);
    }

    private void PromptInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        PlaceholderText.Visibility = string.IsNullOrEmpty(PromptInput.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void SendButton_Click(object sender, RoutedEventArgs e) => Submit();

    private void CollapseButton_Click(object sender, RoutedEventArgs e) => Collapse();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Collapse();

    private async void MicButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isListening)
        {
            _speechCts?.Cancel();
            return;
        }

        if (SpeechInput == null)
        {
            StatusText.Text = "⚠ Voice input is not available.";
            StatusText.Foreground = Brushes.OrangeRed;
            return;
        }

        _isListening = true;
        _speechCts?.Dispose();
        _speechCts = new CancellationTokenSource();
        var token = _speechCts.Token;

        MicButton.Content = "⏹ Stop";
        StatusText.Text = "🎙 Listening… speak your command (auto-submits when done)";
        StatusText.Foreground = Brushes.LightGreen;

        void OnSpeechRecognizing(string partial)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"📝 \"{partial}\"";
                StatusText.Foreground = Brushes.LightSkyBlue;
            });
        }
        SpeechInput.SpeechRecognizing += OnSpeechRecognizing;

        try
        {
            var transcript = await SpeechInput.TranscribeAsync(token);
            SpeechInput.SpeechRecognizing -= OnSpeechRecognizing;

            if (!string.IsNullOrWhiteSpace(transcript))
            {
                PromptInput.Text = transcript;
                StatusText.Text = $"✓ \"{transcript}\" — executing…";
                StatusText.Foreground = Brushes.LightGreen;

                await Task.Delay(350);
                Submit();
            }
            else if (token.IsCancellationRequested)
            {
                StatusText.Text = "Voice recording cancelled.";
                StatusText.Foreground = Brushes.Gray;
            }
            else
            {
                StatusText.Text = "No speech detected. Speak clearly and try again.";
                StatusText.Foreground = Brushes.OrangeRed;
            }
        }
        catch (OperationCanceledException)
        {
            SpeechInput.SpeechRecognizing -= OnSpeechRecognizing;
            StatusText.Text = "Voice recording cancelled.";
            StatusText.Foreground = Brushes.Gray;
        }
        catch (Exception ex)
        {
            SpeechInput.SpeechRecognizing -= OnSpeechRecognizing;
            var msg = ex.Message.StartsWith("⚠") ? ex.Message : "⚠ " + ex.Message;
            StatusText.Text = msg;
            StatusText.Foreground = Brushes.OrangeRed;
        }
        finally
        {
            _isListening = false;
            MicButton.Content = "🎤 Mic";
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (IsExpanded)
            {
                e.Handled = true;
                Submit();
            }
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (IsExpanded)
            {
                Collapse();
            }
            else
            {
                HidePopup();
            }
        }
    }

    private void CollapsedIsland_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 1)
            {
                Expand(_currentTarget);
            }
        }
    }

    private void ExpandedIsland_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void MenuExpand_Click(object sender, RoutedEventArgs e) => Expand(_currentTarget);

    private void MenuRecenter_Click(object sender, RoutedEventArgs e)
    {
        Left = Math.Max(10, (SystemParameters.PrimaryScreenWidth - Width) / 2);
        Top = IsExpanded ? 16 : 8;
    }

    private void MenuHide_Click(object sender, RoutedEventArgs e)
    {
        _speechCts?.Cancel();
        Hide();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
