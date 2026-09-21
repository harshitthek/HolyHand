using System.Windows;
using System.Windows.Input;
using HolyHand.Core.Models;

namespace HolyHand.App.Windows;

public partial class PromptPopupWindow : Window
{
    private AppTarget? _currentTarget;
    public event Action<string, AppTarget?>? TaskSubmitted;
    public event Action? Cancelled;

    public PromptPopupWindow()
    {
        InitializeComponent();
    }

    public void ShowForTarget(AppTarget? target)
    {
        _currentTarget = target;

        if (target != null)
        {
            var title = !string.IsNullOrWhiteSpace(target.WindowTitle) ? target.WindowTitle : target.ProcessName;
            TargetAppText.Text = $"Target: {target.ProcessName} — \"{title}\"";
            ElevatedBadge.Visibility = target.IsElevated ? Visibility.Visible : Visibility.Collapsed;

            if (target.IsElevated)
            {
                StatusText.Text = "⚠️ Target process is running elevated (Admin). UIPI restricts automation.";
                StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
            else
            {
                StatusText.Text = "Press Enter to submit, Esc to cancel";
                StatusText.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }
        else
        {
            TargetAppText.Text = "Target: None";
            ElevatedBadge.Visibility = Visibility.Collapsed;
            StatusText.Text = "Press Enter to submit, Esc to cancel";
            StatusText.Foreground = System.Windows.Media.Brushes.Gray;
        }

        PromptInput.Text = string.Empty;
        Show();
        Activate();
        PromptInput.Focus();
    }

    public void SetExecuting(bool isExecuting, string? status = null)
    {
        Dispatcher.Invoke(() =>
        {
            PromptInput.IsEnabled = !isExecuting;
            SendButton.IsEnabled = !isExecuting;
            SendButton.Content = isExecuting ? "Running…" : "Send ↵";
            if (!string.IsNullOrEmpty(status))
            {
                StatusText.Text = status;
                StatusText.Foreground = System.Windows.Media.Brushes.LightSkyBlue;
            }
        });
    }

    public void UpdateStatus(string status, bool isError = false)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = status;
            StatusText.Foreground = isError 
                ? System.Windows.Media.Brushes.OrangeRed 
                : System.Windows.Media.Brushes.LightSkyBlue;
        });
    }

    public void OnRunCompleted(string message, bool success)
    {
        Dispatcher.Invoke(async () =>
        {
            SetExecuting(false);
            StatusText.Text = (success ? "✅ " : "⚠️ ") + message;
            StatusText.Foreground = success 
                ? System.Windows.Media.Brushes.LightGreen 
                : System.Windows.Media.Brushes.OrangeRed;

            if (success)
            {
                await Task.Delay(1800);
                HidePopup();
            }
        });
    }

    public void HidePopup()
    {
        SetExecuting(false);
        Hide();
        Cancelled?.Invoke();
    }

    private void Submit()
    {
        var goal = PromptInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(goal))
        {
            return;
        }

        if (_currentTarget?.IsElevated == true)
        {
            MessageBox.Show(
                "HolyHand cannot automate an elevated (Administrator) application from a standard user session.\n\nPlease activate a standard window or start HolyHand with administrator rights.",
                "Target is Elevated",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        SetExecuting(true, "Starting automation...");
        TaskSubmitted?.Invoke(goal, _currentTarget);
    }

    private void PromptInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        PlaceholderText.Visibility = string.IsNullOrEmpty(PromptInput.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        Submit();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        HidePopup();
    }

    private void MicButton_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Voice input is scheduled for Milestone M7.";
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Submit();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            HidePopup();
        }
    }
}
