using System.Text.RegularExpressions;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Models;
using Microsoft.Extensions.Logging;

namespace HolyHand.Core.Safety;

public class RiskPolicy : IRiskPolicy
{
    private readonly RiskPolicyOptions _options;
    private readonly ILogger<RiskPolicy> _logger;
    private readonly Regex _sensitiveVerbRegex;
    private readonly Regex _prohibitedRegex;

    public RiskPolicy(RiskPolicyOptions? options = null, ILogger<RiskPolicy>? logger = null)
    {
        _options = options ?? new RiskPolicyOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RiskPolicy>.Instance;

        // Build word-boundary regex from configured sensitive verbs
        var patterns = _options.SensitiveVerbs.Select(Regex.Escape);
        _sensitiveVerbRegex = new Regex(
            $@"\b({string.Join("|", patterns)})\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Build word-boundary regex from configured prohibited deletion terms
        var prohibitedPatterns = _options.ProhibitedTerms.Select(Regex.Escape);
        _prohibitedRegex = new Regex(
            $@"\b({string.Join("|", prohibitedPatterns)})\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

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

        // 2. Pure navigation actions are harmless
        if (decision.Operation is AgentOperation.ScrollDown or AgentOperation.ScrollUp
            or AgentOperation.PressTab or AgentOperation.PressEscape)
        {
            reason = string.Empty;
            return false;
        }

        // 3. Inspect target element labels and decision descriptions
        var textToInspect = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(decision.TargetLabel))
        {
            textToInspect.Add(decision.TargetLabel);
        }
        if (target != null)
        {
            if (!string.IsNullOrWhiteSpace(target.Label)) textToInspect.Add(target.Label);
            if (!string.IsNullOrWhiteSpace(target.Value)) textToInspect.Add(target.Value);
            if (!string.IsNullOrWhiteSpace(target.Role)) textToInspect.Add(target.Role);
        }

        // Check for prohibited or sensitive verb matches in target text
        foreach (var text in textToInspect)
        {
            var prohibitedMatch = _prohibitedRegex.Match(text);
            if (prohibitedMatch.Success)
            {
                reason = $"Action '{decision.Operation}' on '{target?.DisplayLabel ?? decision.TargetLabel}' matches prohibited verb '{prohibitedMatch.Value}'.";
                _logger.LogInformation("Confirmation required: {Reason}", reason);
                return true;
            }

            var match = _sensitiveVerbRegex.Match(text);
            if (match.Success)
            {
                reason = $"Action '{decision.Operation}' on '{target?.DisplayLabel ?? decision.TargetLabel}' matches sensitive verb '{match.Value}'.";
                _logger.LogInformation("Confirmation required: {Reason}", reason);
                return true;
            }
        }

        // 4. Password / secret fields check
        if (target != null)
        {
            if (target.Role.Equals("PasswordBox", StringComparison.OrdinalIgnoreCase)
                || target.Value == "[PASSWORD]"
                || target.Label.Contains("password", StringComparison.OrdinalIgnoreCase)
                || target.Label.Contains("pin", StringComparison.OrdinalIgnoreCase)
                || target.Label.Contains("ssn", StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Interacting with sensitive/password field '{target.DisplayLabel}' requires confirmation.";
                _logger.LogInformation("Confirmation required: {Reason}", reason);
                return true;
            }
        }

        // 5. Typing sensitive credentials or tokens
        if (decision.Operation == AgentOperation.TypeText && !string.IsNullOrEmpty(decision.TextValue))
        {
            var prohibitedMatch = _prohibitedRegex.Match(decision.TextValue);
            if (prohibitedMatch.Success)
            {
                reason = $"Typed text contains prohibited verb '{prohibitedMatch.Value}'.";
                _logger.LogInformation("Confirmation required: {Reason}", reason);
                return true;
            }

            var textMatch = _sensitiveVerbRegex.Match(decision.TextValue);
            if (textMatch.Success)
            {
                reason = $"Typed text contains sensitive verb '{textMatch.Value}'.";
                _logger.LogInformation("Confirmation required: {Reason}", reason);
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }

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
            reason = $"⛔ Prohibited by safety policy: Deletion tasks (matching '{match.Value}') are strictly prohibited.";
            _logger.LogWarning("Goal prohibited by policy: {Reason}", reason);
            return true;
        }

        reason = string.Empty;
        return false;
    }

    public bool IsActionProhibited(AgentDecision decision, AccessibilityElement? target, string goal, out string reason)
    {
        var textToInspect = new List<string>(5);
        if (!string.IsNullOrWhiteSpace(decision.TargetLabel)) textToInspect.Add(decision.TargetLabel);
        if (target != null)
        {
            if (!string.IsNullOrWhiteSpace(target.Label)) textToInspect.Add(target.Label);
            if (!string.IsNullOrWhiteSpace(target.Value)) textToInspect.Add(target.Value);
            if (!string.IsNullOrWhiteSpace(target.Role)) textToInspect.Add(target.Role);
        }
        if (decision.Operation == AgentOperation.TypeText && !string.IsNullOrWhiteSpace(decision.TextValue))
        {
            textToInspect.Add(decision.TextValue);
        }

        foreach (var text in textToInspect)
        {
            var match = _prohibitedRegex.Match(text);
            if (match.Success)
            {
                reason = $"⛔ Prohibited by safety policy: Action '{decision.Operation}' on '{target?.DisplayLabel ?? decision.TargetLabel}' matches prohibited deletion term '{match.Value}'.";
                _logger.LogWarning("Action prohibited by policy: {Reason}", reason);
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }
}
