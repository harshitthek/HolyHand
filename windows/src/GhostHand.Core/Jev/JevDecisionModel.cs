using System.Text.RegularExpressions;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Models;
using Microsoft.Extensions.Logging;

namespace HolyHand.Core.Jev;

public class JevDecisionModel : IDecisionModel
{
    private readonly IJevClient _jevClient;
    private readonly JevOptions _options;
    private readonly ILogger<JevDecisionModel> _logger;

    public JevDecisionModel(IJevClient jevClient, JevOptions options, ILogger<JevDecisionModel> logger)
    {
        _jevClient = jevClient;
        _options = options;
        _logger = logger;
    }

    public async Task<AgentDecision> DecideNextActionAsync(
        string goal,
        AppTarget target,
        IReadOnlyList<AccessibilityElement> elements,
        IReadOnlyList<string> history,
        CancellationToken cancellationToken = default)
    {
        // 1. Build candidate action choices deterministically
        var candidateChoices = BuildCandidateChoices(goal, elements);

        // 2. Build compact state object (text-only, no secrets)
        var state = new
        {
            task = Truncate(goal, 4000),
            app = Truncate(target.ProcessName, 100),
            window = Truncate(target.WindowTitle, 150),
            step = history.Count + 1,
            actionAttempts = history.Count > 0 ? history.TakeLast(8).ToList() : new List<string> { "nothing yet" },
            elementCount = elements.Count,
            elements = elements.Take(40).Select(e => new
            {
                id = e.Id,
                role = e.DisplayRole,
                label = Truncate(e.DisplayLabel, 160),
                enabled = e.Enabled,
                focused = e.Focused,
                source = e.Source
            }).ToList()
        };

        // 3. Build questions: Call A
        var questions = new Dictionary<string, QuestionDefinition>
        {
            ["nextAction"] = QuestionDefinition.Choice(
                candidateChoices,
                $"Select the single best next action to advance toward: \"{goal}\""),
            ["goalAchieved"] = QuestionDefinition.Boolean(
                $"Has the user's task \"{goal}\" already been completely fulfilled by the current screen state?")
        };

        var request = new EvaluateRequest
        {
            Model = _options.ModelId,
            State = state,
            Questions = questions,
            ProviderOptions = new GatewayProviderOptions
            {
                Gateway = new GatewayOptions
                {
                    ZeroDataRetention = _options.ZeroDataRetention ? true : null,
                    Only = new List<string> { "typesafe-ai" }
                }
            }
        };

        var response = await _jevClient.EvaluateAsync(request, cancellationToken);

        // Check if goal is already completed
        if (response.TryGetBooleanAnswer("goalAchieved", out var goalProb, out var isAchieved) && isAchieved && goalProb >= _options.DecisionConfidenceThreshold)
        {
            return new AgentDecision
            {
                Operation = AgentOperation.Done,
                Reason = $"Goal achieved (confidence: {goalProb:P0})",
                Confidence = goalProb
            };
        }

        // Parse next action choice
        if (!response.TryGetChoiceAnswer("nextAction", out var chosenKey, out var confidence, out var probabilities))
        {
            _logger.LogWarning("Failed to parse nextAction choice from Jev response. Asking user.");
            return new AgentDecision { Operation = AgentOperation.AskUser, Reason = "Could not parse decision" };
        }

        // 4. Confidence gate: if top probability is below threshold, ask user
        if (confidence < _options.DecisionConfidenceThreshold)
        {
            _logger.LogInformation("Top action '{Choice}' confidence {Conf:P0} below threshold {Thresh:P0}. Asking user.",
                chosenKey, confidence, _options.DecisionConfidenceThreshold);

            return new AgentDecision
            {
                Operation = AgentOperation.AskUser,
                Reason = $"Confidence {confidence:P0} is below threshold {_options.DecisionConfidenceThreshold:P0}",
                Confidence = confidence
            };
        }

        return ParseActionDecision(chosenKey, confidence, elements);
    }

    public async Task<bool> VerifyCompletionAsync(
        string goal,
        AppTarget target,
        IReadOnlyList<AccessibilityElement> elements,
        IReadOnlyList<string> history,
        CancellationToken cancellationToken = default)
    {
        var state = new
        {
            task = Truncate(goal, 4000),
            app = Truncate(target.ProcessName, 100),
            window = Truncate(target.WindowTitle, 150),
            actionAttempts = history.TakeLast(6).ToList(),
            visibleControls = elements.Take(30).Select(e => $"{e.DisplayRole}: \"{e.DisplayLabel}\"").ToList()
        };

        var request = new EvaluateRequest
        {
            Model = _options.ModelId,
            State = state,
            Questions = new Dictionary<string, QuestionDefinition>
            {
                ["done"] = QuestionDefinition.Boolean(
                    $"Are ALL requirements of the task \"{goal}\" completely satisfied based on visible controls and recorded actions?")
            },
            ProviderOptions = new GatewayProviderOptions
            {
                Gateway = new GatewayOptions { ZeroDataRetention = _options.ZeroDataRetention ? true : null }
            }
        };

        var response = await _jevClient.EvaluateAsync(request, cancellationToken);
        if (response.TryGetBooleanAnswer("done", out var prob, out var isDone))
        {
            return isDone && prob >= _options.DecisionConfidenceThreshold;
        }

        return false;
    }

    private static Dictionary<string, string> BuildCandidateChoices(string goal, IReadOnlyList<AccessibilityElement> elements)
    {
        var choices = new Dictionary<string, string>();

        // Extract search/literal phrase from user goal
        var textCandidate = ExtractSearchPhrase(goal);

        int clickCount = 0;
        foreach (var el in elements)
        {
            if (!el.Enabled) continue;

            if (IsClickable(el.Role) && clickCount < 25)
            {
                clickCount++;
                var key = $"click:{el.Id}";
                var desc = $"Click {el.DisplayRole} \"{el.DisplayLabel}\"";
                choices[key] = desc;
            }

            if (IsTypeable(el.Role) && !string.IsNullOrEmpty(textCandidate))
            {
                var key = $"type:{el.Id}:{textCandidate}";
                var desc = $"Type \"{textCandidate}\" into {el.DisplayRole} \"{el.DisplayLabel}\"";
                choices[key] = desc;
            }
        }

        // Standard actions
        choices["press:enter"] = "Press Enter/Return key";
        choices["press:tab"] = "Press Tab key to advance focus";
        choices["press:escape"] = "Press Escape key to dismiss dialog/menu";
        choices["scroll:down"] = "Scroll down to reveal more controls";
        choices["scroll:up"] = "Scroll up";
        choices["wait"] = "Wait 1 second for UI to update";
        choices["done"] = "Task is completely finished";
        choices["ask_user"] = "Need human guidance or clarification";

        return choices;
    }

    private static AgentDecision ParseActionDecision(string key, double confidence, IReadOnlyList<AccessibilityElement> elements)
    {
        if (key.StartsWith("click:", StringComparison.OrdinalIgnoreCase))
        {
            var elementId = key[6..];
            var el = elements.FirstOrDefault(e => e.Id == elementId);
            return new AgentDecision
            {
                Operation = AgentOperation.Click,
                TargetId = elementId,
                TargetLabel = el?.DisplayLabel,
                Confidence = confidence,
                Reason = $"Click element {elementId}"
            };
        }

        if (key.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = key.Split(':', 3);
            var elementId = parts.Length > 1 ? parts[1] : string.Empty;
            var text = parts.Length > 2 ? parts[2] : string.Empty;
            var el = elements.FirstOrDefault(e => e.Id == elementId);

            return new AgentDecision
            {
                Operation = AgentOperation.TypeText,
                TargetId = elementId,
                TargetLabel = el?.DisplayLabel,
                TextValue = text,
                Confidence = confidence,
                Reason = $"Type '{text}' into {elementId}"
            };
        }

        return key.ToLowerInvariant() switch
        {
            "press:enter" => new AgentDecision { Operation = AgentOperation.PressReturn, Confidence = confidence },
            "press:tab" => new AgentDecision { Operation = AgentOperation.PressTab, Confidence = confidence },
            "press:escape" => new AgentDecision { Operation = AgentOperation.PressEscape, Confidence = confidence },
            "scroll:down" => new AgentDecision { Operation = AgentOperation.ScrollDown, Confidence = confidence },
            "scroll:up" => new AgentDecision { Operation = AgentOperation.ScrollUp, Confidence = confidence },
            "wait" => new AgentDecision { Operation = AgentOperation.Wait, Confidence = confidence },
            "done" => new AgentDecision { Operation = AgentOperation.Done, Confidence = confidence },
            _ => new AgentDecision { Operation = AgentOperation.AskUser, Confidence = confidence }
        };
    }

    private static bool IsClickable(string role) =>
        role.Equals("Button", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("MenuItem", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("TabItem", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("Hyperlink", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("CheckBox", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("RadioButton", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("ComboBox", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("ListItem", StringComparison.OrdinalIgnoreCase);

    private static bool IsTypeable(string role) =>
        role.Equals("Edit", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("Document", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("ComboBox", StringComparison.OrdinalIgnoreCase);

    private static string ExtractSearchPhrase(string goal)
    {
        // 1. Check for quoted text: search for "Adele" or type "Hello"
        var quoteMatch = Regex.Match(goal, "\"([^\"]+)\"");
        if (quoteMatch.Success)
            return quoteMatch.Groups[1].Value;

        // 2. Check for search for ... pattern
        var searchMatch = Regex.Match(goal, @"(?:search|look)\s+for\s+(.+)$", RegexOptions.IgnoreCase);
        if (searchMatch.Success)
            return searchMatch.Groups[1].Value.Trim();

        return string.Empty;
    }

    private static string Truncate(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars) return text;
        return text[..maxChars];
    }
}
