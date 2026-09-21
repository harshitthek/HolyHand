using System.Drawing;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Models;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace HolyHand.Platform.Execution;

public class ActionExecutor : IActionExecutor, IDisposable
{
    private readonly ILogger<ActionExecutor> _logger;
    private readonly UIA3Automation _automation;
    private bool _disposed;

    public bool DryRun { get; set; } = true;
    public int? ExpectedProcessId { get; set; }
    public IntPtr? TargetWindowHandle { get; set; }

    public ActionExecutor(ILogger<ActionExecutor> logger, bool dryRun = true)
    {
        _logger = logger;
        DryRun = dryRun;
        _automation = new UIA3Automation();
    }

    public async Task<ActionResult> ExecuteAsync(
        AgentDecision decision,
        AccessibilityElement? targetElement,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (decision.Operation == AgentOperation.Done)
        {
            return ActionResult.SuccessResult("Task completed");
        }

        if (decision.Operation == AgentOperation.AskUser)
        {
            return ActionResult.SuccessResult("User consultation requested");
        }

        if (decision.Operation == AgentOperation.Wait)
        {
            await Task.Delay(1000, cancellationToken);
            return ActionResult.SuccessResult("Waited 1 second");
        }

        // Dry-run mode: plan and verify without injecting hardware input
        if (DryRun)
        {
            _logger.LogInformation("[DRY RUN] Would execute {Operation} on '{Target}' with value '{Value}'",
                decision.Operation, decision.TargetLabel ?? decision.TargetId, decision.TextValue);

            // Simulate tiny execution delay
            await Task.Delay(150, cancellationToken);
            return ActionResult.SuccessResult($"[DRY RUN] Simulated {decision.Operation} on '{decision.TargetLabel ?? decision.TargetId}'");
        }

        // Re-focus target window before physical execution
        if (TargetWindowHandle.HasValue && TargetWindowHandle.Value != IntPtr.Zero)
        {
            PInvoke.SetForegroundWindow((HWND)TargetWindowHandle.Value);
            await Task.Delay(60, cancellationToken);
        }

        // Live execution mode: verify foreground process matches expected target (UIPI / safety check)
        if (ExpectedProcessId.HasValue)
        {
            uint currentPid = GetForegroundProcessId();

            if (currentPid != (uint)ExpectedProcessId.Value)
            {
                _logger.LogWarning("Foreground process changed mid-action! Expected PID {Expected}, current PID {Current}. Aborting.",
                    ExpectedProcessId.Value, currentPid);

                return ActionResult.FailureResult(
                    $"Foreground process changed mid-action (expected PID {ExpectedProcessId.Value}, found {currentPid}). Execution aborted.");
            }
        }

        try
        {
            switch (decision.Operation)
            {
                case AgentOperation.Click:
                    return ExecuteClick(targetElement);

                case AgentOperation.TypeText:
                    return ExecuteTypeText(targetElement, decision.TextValue ?? string.Empty);

                case AgentOperation.PressReturn:
                    InputSimulator.SendKey(VIRTUAL_KEY.VK_RETURN);
                    return ActionResult.SuccessResult("Pressed Enter key");

                case AgentOperation.PressTab:
                    InputSimulator.SendKey(VIRTUAL_KEY.VK_TAB);
                    return ActionResult.SuccessResult("Pressed Tab key");

                case AgentOperation.PressEscape:
                    InputSimulator.SendKey(VIRTUAL_KEY.VK_ESCAPE);
                    return ActionResult.SuccessResult("Pressed Escape key");

                case AgentOperation.ScrollDown:
                    InputSimulator.Scroll(down: true);
                    return ActionResult.SuccessResult("Scrolled down");

                case AgentOperation.ScrollUp:
                    InputSimulator.Scroll(down: false);
                    return ActionResult.SuccessResult("Scrolled up");

                default:
                    return ActionResult.FailureResult($"Unsupported operation '{decision.Operation}'");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute {Operation} on target {Target}", decision.Operation, decision.TargetId);
            return ActionResult.FailureResult(ex.Message);
        }
    }

    private ActionResult ExecuteClick(AccessibilityElement? targetElement)
    {
        if (targetElement == null || targetElement.Frame.IsEmpty)
        {
            return ActionResult.FailureResult("Cannot click: target element not found or has empty bounding frame.");
        }

        // Try UIA patterns first via element point
        var center = new Point(
            targetElement.Frame.X + targetElement.Frame.Width / 2,
            targetElement.Frame.Y + targetElement.Frame.Height / 2);

        try
        {
            var uiaElement = _automation.FromPoint(center);
            var current = uiaElement;
            int depth = 0;
            while (current != null && depth < 5)
            {
                if (current.Patterns.Invoke.IsSupported)
                {
                    current.Patterns.Invoke.Pattern.Invoke();
                    _logger.LogDebug("Clicked via UIA InvokePattern on {Label}", targetElement.DisplayLabel);
                    return ActionResult.SuccessResult($"Clicked via InvokePattern on '{targetElement.DisplayLabel}'");
                }

                if (current.Patterns.Toggle.IsSupported)
                {
                    current.Patterns.Toggle.Pattern.Toggle();
                    _logger.LogDebug("Toggled via UIA TogglePattern on {Label}", targetElement.DisplayLabel);
                    return ActionResult.SuccessResult($"Toggled via TogglePattern on '{targetElement.DisplayLabel}'");
                }

                if (current.Patterns.SelectionItem.IsSupported)
                {
                    current.Patterns.SelectionItem.Pattern.Select();
                    _logger.LogDebug("Selected via UIA SelectionItemPattern on {Label}", targetElement.DisplayLabel);
                    return ActionResult.SuccessResult($"Selected via SelectionItemPattern on '{targetElement.DisplayLabel}'");
                }

                try
                {
                    current = current.Parent;
                    depth++;
                }
                catch
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "UIA pattern invocation failed; falling back to SendInput mouse click.");
        }

        // Fallback: simulated mouse click at center of element
        InputSimulator.Click(center.X, center.Y);
        _logger.LogDebug("Clicked via SendInput fallback at ({X}, {Y})", center.X, center.Y);
        return ActionResult.SuccessResult($"Clicked via SendInput fallback at ({center.X}, {center.Y})");
    }

    private ActionResult ExecuteTypeText(AccessibilityElement? targetElement, string text)
    {
        if (targetElement == null)
        {
            // Type into currently focused control
            InputSimulator.TypeText(text);
            return ActionResult.SuccessResult($"Typed text into focused element");
        }

        var center = new Point(
            targetElement.Frame.X + targetElement.Frame.Width / 2,
            targetElement.Frame.Y + targetElement.Frame.Height / 2);

        // Try UIA ValuePattern first
        try
        {
            var uiaElement = _automation.FromPoint(center);
            var current = uiaElement;
            int depth = 0;
            while (current != null && depth < 5)
            {
                if (current.Patterns.Value.IsSupported)
                {
                    current.Patterns.Value.Pattern.SetValue(text);
                    _logger.LogDebug("Typed '{Text}' via UIA ValuePattern on {Label}", text, targetElement.DisplayLabel);
                    return ActionResult.SuccessResult($"Typed text via ValuePattern on '{targetElement.DisplayLabel}'");
                }

                try
                {
                    current = current.Parent;
                    depth++;
                }
                catch
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "UIA ValuePattern typing failed; falling back to click-and-type.");
        }

        // Fallback: Click to focus, then SendInput text
        InputSimulator.Click(center.X, center.Y);
        Thread.Sleep(50);
        InputSimulator.TypeText(text);
        return ActionResult.SuccessResult($"Typed text via SendInput fallback on '{targetElement.DisplayLabel}'");
    }

    private static uint GetForegroundProcessId()
    {
        var fgHwnd = PInvoke.GetForegroundWindow();
        uint currentPid = 0;
        unsafe
        {
            PInvoke.GetWindowThreadProcessId(fgHwnd, &currentPid);
        }
        return currentPid;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _automation.Dispose();
        }
    }
}
