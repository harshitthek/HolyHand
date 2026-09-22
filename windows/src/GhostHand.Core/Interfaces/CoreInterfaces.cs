using HolyHand.Core.Models;
using HolyHand.Core.Safety;

namespace HolyHand.Core.Interfaces;

public interface IScreenReader
{
    Task<IReadOnlyList<AccessibilityElement>> ReadElementsAsync(AppTarget target, CancellationToken cancellationToken = default);
}

public interface IDecisionModel
{
    Task<AgentDecision> DecideNextActionAsync(
        string goal,
        AppTarget target,
        IReadOnlyList<AccessibilityElement> elements,
        IReadOnlyList<string> history,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyCompletionAsync(
        string goal,
        AppTarget target,
        IReadOnlyList<AccessibilityElement> elements,
        IReadOnlyList<string> history,
        CancellationToken cancellationToken = default);

    Task<ActionRiskScore> EvaluateActionRiskAsync(
        string goal,
        AppTarget target,
        AgentDecision decision,
        AccessibilityElement? targetElement,
        CancellationToken cancellationToken = default);
}

public interface IActionExecutor
{
    Task<ActionResult> ExecuteAsync(
        AgentDecision decision,
        AccessibilityElement? targetElement,
        CancellationToken cancellationToken = default);
}

public interface IHotkeyService : IDisposable
{
    event EventHandler? HotkeyPressed;
    event EventHandler? KillSwitchTriggered;
    void Start();
    void Stop();
}

public interface IRiskPolicy
{
    bool IsAppDenied(AppTarget appTarget, out string reason);
    bool RequiresConfirmation(AgentDecision decision, AccessibilityElement? target, AppTarget appTarget, out string reason);
}

public interface IConfirmationPrompt
{
    Task<bool> RequestConfirmationAsync(
        AgentDecision decision,
        AccessibilityElement? target,
        AppTarget appTarget,
        string reason,
        CancellationToken cancellationToken = default);
}

public interface IAuditLog
{
    Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}

public interface ICredentialStore
{
    string? GetApiKey();
    void SetApiKey(string apiKey);
    void DeleteApiKey();
}

public interface ISpeechInput : IDisposable
{
    Task<string> TranscribeAsync(CancellationToken cancellationToken = default);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default);
}

public interface IAppLauncher
{
    bool TryExtractAppLaunch(string goal, out string appName, out string launchCommand);
    bool TryExtractUrlLaunch(string goal, out Uri url);
    Task<AppTarget?> LaunchAppAsync(string appName, string? launchCommand = null, CancellationToken cancellationToken = default);
    Task<AppTarget?> LaunchUrlAsync(Uri url, CancellationToken cancellationToken = default);
}

