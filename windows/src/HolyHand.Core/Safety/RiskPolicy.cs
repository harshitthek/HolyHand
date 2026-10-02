using System.Text.RegularExpressions;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Models;
using Microsoft.Extensions.Logging;

namespace HolyHand.Core.Safety;

/// <summary>
/// Guardian-mode risk policy: enforces human confirmation gates for sensitive and irreversible actions,
/// blocks data deletion operations permanently, and auto-executes harmless routine navigation.
/// </summary>
public class RiskPolicy : IRiskPolicy
{
    private static readonly Regex SensitiveFieldRegex = new(
        @"\b(password|pin|ssn|social security)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly RiskPolicyOptions _options;
    private readonly ILogger<RiskPolicy> _logger;
    private readonly Regex _prohibitedRegex;
    private readonly Regex _sensitiveRegex;

    public RiskPolicy(RiskPolicyOptions? options = null, ILogger<RiskPolicy>? logger = null)
    {
        _options = options ?? new RiskPolicyOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RiskPolicy>.Instance;

        // Build word-boundary regex for deletion terms only
        if (_options.ProhibitedTerms.Count > 0)
        {
            var prohibitedPatterns = _options.ProhibitedTerms.Select(Regex.Escape);
            _prohibitedRegex = new Regex(
                $@"\b({string.Join("|", prohibitedPatterns)})\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
        else
        {
            _prohibitedRegex = new Regex(@"^$", RegexOptions.Compiled); // never matches
        }

        // Build word-boundary regex for sensitive action verbs
        if (_options.SensitiveVerbs.Count > 0)
        {
            var sensitivePatterns = _options.SensitiveVerbs.Select(Regex.Escape);
            _sensitiveRegex = new Regex(
                $@"\b({string.Join("|", sensitivePatterns)})\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
        else
        {
            _sensitiveRegex = new Regex(@"^$", RegexOptions.Compiled);
        }
    }

    /// <summary>
    /// Checks if the target application process is on the deny-list (e.g. password managers).
    /// </summary>
    public bool IsAppDenied(AppTarget appTarget, out string reason)
    {
        if (_options.DenyListedProcesses.Contains(appTarget.ProcessName))
        {
            reason = $"Application process '{appTarget.ProcessName}' is on the security deny-list.";
            _logger.LogWarning("Security deny-list triggered: {Reason}", reason);
            return true;
        }

        foreach (var denied in _options.DenyListedProcesses)
        {
            if (appTarget.WindowTitle.Contains(denied, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Window title '{appTarget.WindowTitle}' matches deny-listed application '{denied}'.";
                _logger.LogWarning("Security deny-list triggered: {Reason}", reason);
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }

    /// <summary>
    /// Checks if a proposed action requires human confirmation.
    /// Actions matching sensitive verbs (e.g. Submit, Pay, Buy, Apply, Send) or targeting sensitive fields
    /// unconditionally require human approval before execution.
    /// </summary>
    public bool RequiresConfirmation(
        AgentDecision decision,
        AccessibilityElement? target,
        AppTarget appTarget,
        out string reason)
    {
        // 1. Benign agent control flow never requires confirmation
        if (decision.Operation is AgentOperation.Done or AgentOperation.AskUser or AgentOperation.Wait)
        {
            reason = string.Empty;
            return false;
        }

        // 2. Pure navigation and harmless system/volume/media actions never require confirmation
        if (decision.Operation is AgentOperation.ScrollDown or AgentOperation.ScrollUp
            or AgentOperation.PressTab or AgentOperation.PressEscape
            or AgentOperation.CheckVolume or AgentOperation.VolumeUp or AgentOperation.VolumeDown
            or AgentOperation.VolumeMute or AgentOperation.VolumeSet
            or AgentOperation.MediaPlayPause or AgentOperation.MediaNext or AgentOperation.MediaPrevious
            or AgentOperation.LockWorkstation or AgentOperation.ShowDesktop or AgentOperation.TakeScreenshot)
        {
            reason = string.Empty;
            return false;
        }

        // 3. Model risk score escalation check
        if (decision.RiskScore >= _options.EscalateOnRiskScore)
        {
            reason = $"Action risk score '{decision.RiskScore}' meets or exceeds confirmation threshold '{_options.EscalateOnRiskScore}'.";
            _logger.LogInformation("Action requires confirmation (model risk escalation): {Reason}", reason);
            return true;
        }

        // 4. Password / secret fields check (uses word boundaries to prevent 'Spotify pinned' false positive)
        if (target != null)
        {
            if (target.Role.Equals("PasswordBox", StringComparison.OrdinalIgnoreCase)
                || target.Value == "[PASSWORD]"
                || SensitiveFieldRegex.IsMatch(target.Label))
            {
                reason = $"Interacting with sensitive/password field '{target.DisplayLabel}' requires confirmation.";
                _logger.LogInformation("Action requires confirmation (sensitive field): {Reason}", reason);
                return true;
            }
        }

        // 5. Inspect target element labels and decision descriptions for sensitive verbs
        var textToInspect = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(decision.TargetLabel))
        {
            textToInspect.Add(decision.TargetLabel);
        }
        if (target != null)
        {
            if (!string.IsNullOrWhiteSpace(target.Label)) textToInspect.Add(target.Label);
            if (!string.IsNullOrWhiteSpace(target.Value)) textToInspect.Add(target.Value);
        }

        foreach (var text in textToInspect)
        {
            var match = _sensitiveRegex.Match(text);
            if (match.Success)
            {
                reason = $"Action '{decision.Operation}' on '{target?.DisplayLabel ?? decision.TargetLabel}' matches sensitive verb '{match.Value}'.";
                _logger.LogInformation("Action requires confirmation: {Reason}", reason);
                return true;
            }
        }

        // 6. Typing sensitive verbs or text
        if (_options.RequireConfirmationOnSensitiveText && decision.Operation == AgentOperation.TypeText && !string.IsNullOrWhiteSpace(decision.TextValue))
        {
            var textMatch = _sensitiveRegex.Match(decision.TextValue);
            if (textMatch.Success)
            {
                reason = $"Typed text contains sensitive verb '{textMatch.Value}'.";
                _logger.LogInformation("Action requires confirmation: {Reason}", reason);
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }

    /// <summary>
    /// Checks if the user''s goal contains deletion instructions. If so, block the entire task.
    /// </summary>
    public bool IsGoalProhibited(string goal, out string reason)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            reason = string.Empty;
            return false;
        }

        var match = _prohibitedRegex.Match(goal);
        if (match.Success)
        {
            reason = $"Prohibited by safety policy: Deletion tasks (matching ''{match.Value}'') are strictly prohibited.";
            _logger.LogWarning("Goal prohibited by policy: {Reason}", reason);
            return true;
        }

        reason = string.Empty;
        return false;
    }

    /// <summary>
    /// Checks if a specific action targets a deletion operation. If so, block it mid-task.
    /// Uses regex to match deletion terms in element labels and typed text.
    /// </summary>
    public bool IsActionProhibited(AgentDecision decision, AccessibilityElement? target, string goal, out string reason)
    {
        // Collect all text to inspect
        var textToInspect = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(decision.TargetLabel)) textToInspect.Add(decision.TargetLabel);
        if (target != null)
        {
            if (!string.IsNullOrWhiteSpace(target.Label)) textToInspect.Add(target.Label);
            if (!string.IsNullOrWhiteSpace(target.Value)) textToInspect.Add(target.Value);
        }
        if (decision.Operation == AgentOperation.TypeText && !string.IsNullOrWhiteSpace(decision.TextValue))
        {
            textToInspect.Add(decision.TextValue);
        }

        // Use regex to find any deletion term in the text
        foreach (var text in textToInspect)
        {
            var match = _prohibitedRegex.Match(text);
            if (match.Success)
            {
                reason = $"Prohibited by safety policy: Action ''{decision.Operation}'' on ''{target?.DisplayLabel ?? decision.TargetLabel}'' matches deletion term ''{match.Value}''.";
                _logger.LogWarning("Action prohibited by policy: {Reason}", reason);
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }
}
