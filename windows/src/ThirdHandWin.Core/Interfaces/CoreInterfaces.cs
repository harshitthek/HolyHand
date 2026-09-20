using HolyHand.Core.Models;

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
    bool RequiresConfirmation(AgentDecision decision, AccessibilityElement? target, AppTarget appTarget, out string reason);
}

public interface IAuditLog
{
    Task LogAsync(string goal, AgentDecision decision, string decisionType, CancellationToken cancellationToken = default);
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
