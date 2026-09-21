using System.Windows;
using System.Windows.Input;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Models;

namespace HolyHand.App.Windows;

public partial class ConfirmationDialog : Window, IConfirmationPrompt
{
    private TaskCompletionSource<bool>? _tcs;

    public string ActionText => TxtAction.Text;
    public string TargetText => TxtTarget.Text;
    public string AppText => TxtApp.Text;
    public string ReasonText => TxtReason.Text;

    public ConfirmationDialog()
    {
        InitializeComponent();
    }

    public void SetDetails(AgentDecision decision, AccessibilityElement? target, AppTarget appTarget, string reason)
    {
        TxtAction.Text = decision.Operation.ToString();
        TxtTarget.Text = $"{target?.DisplayLabel ?? decision.TargetLabel ?? decision.TargetId} ({target?.DisplayRole ?? "Control"})";
        TxtApp.Text = $"{appTarget.ProcessName} — \"{appTarget.WindowTitle}\"";
        TxtReason.Text = reason;
    }

    public Task<bool> RequestConfirmationAsync(
        AgentDecision decision,
        AccessibilityElement? target,
        AppTarget appTarget,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return Dispatcher.InvokeAsync(() =>
        {
            _tcs = new TaskCompletionSource<bool>();
            SetDetails(decision, target, appTarget, reason);

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        if (_tcs != null && !_tcs.Task.IsCompleted)
                        {
                            _tcs.TrySetResult(false);
                            Hide();
                        }
                    });
                });
            }

            Show();
            Activate();
            BtnApprove.Focus();

            return _tcs.Task;
        }).Result;
    }

    private void BtnApprove_Click(object sender, RoutedEventArgs e)
    {
        _tcs?.TrySetResult(true);
        Hide();
    }

    private void BtnReject_Click(object sender, RoutedEventArgs e)
    {
        _tcs?.TrySetResult(false);
        Hide();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            BtnReject_Click(sender, e);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            BtnApprove_Click(sender, e);
        }
    }
}
