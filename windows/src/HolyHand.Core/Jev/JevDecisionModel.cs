using System.Text.RegularExpressions;
using HolyHand.Core.Agent;
using HolyHand.Core.Common;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Models;
using HolyHand.Core.Safety;
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
        // Check for conversational or non-task prompts (e.g. greetings)
        if (IsConversationalOrGreeting(goal))
        {
            return new AgentDecision
            {
                Operation = AgentOperation.AskUser,
                Reason = "HolyHand automates desktop tasks. Try asking to: 'open notepad', 'check system volume', 'mute', or click a button in the focused window."
            };
        }

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

        // Confidence gate: if top probability is below threshold, ask user
        if (_options.DecisionConfidenceThreshold > 0 && confidence < _options.DecisionConfidenceThreshold)
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

        // Execute chosen action directly as decided by the Jev model
        _logger.LogInformation("Jev selected action: '{Choice}' (probability: {Conf:P0})", chosenKey, confidence);
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

    public async Task<ActionRiskScore> EvaluateActionRiskAsync(
        string goal,
        AppTarget target,
        AgentDecision decision,
        AccessibilityElement? targetElement,
        CancellationToken cancellationToken = default)
    {
        // Benign agent control flow actions are harmless
        if (decision.Operation is AgentOperation.Done or AgentOperation.AskUser or AgentOperation.Wait)
        {
            return ActionRiskScore.Harmless;
        }

        var state = new
        {
            task = Truncate(goal, 4000),
            app = Truncate(target.ProcessName, 100),
            window = Truncate(target.WindowTitle, 150),
            proposedAction = new
            {
                operation = decision.Operation.ToString(),
                targetId = decision.TargetId,
                targetRole = targetElement?.DisplayRole ?? "Unknown",
                targetLabel = Truncate(targetElement?.DisplayLabel ?? decision.TargetLabel ?? "None", 200),
                textValue = decision.TextValue != null ? Truncate(decision.TextValue, 100) : null
            }
        };

        var request = new EvaluateRequest
        {
            Model = _options.ModelId,
            State = state,
            Questions = new Dictionary<string, QuestionDefinition>
            {
                ["actionRisk"] = QuestionDefinition.Score(
                    new List<string> { "harmless", "reversible edit", "irreversible or external effect" },
                    "Rate the risk of this proposed user-interface action from lowest (harmless UI navigation) to highest (irreversible external effect, submission, payment, deletion, or external data transmission).")
            },
            ProviderOptions = new GatewayProviderOptions
            {
                Gateway = new GatewayOptions { ZeroDataRetention = _options.ZeroDataRetention ? true : null }
            }
        };

        try
        {
            var response = await _jevClient.EvaluateAsync(request, cancellationToken);
            if (response.TryGetScoreAnswer("actionRisk", out var score, out var probabilities))
            {
                _logger.LogInformation("Jev evaluated action risk: Score {Score}, probabilities: [{Probs}]",
                    score, string.Join(", ", probabilities.Select(p => $"{p:P0}")));

                if (score >= 3 || (score == 2 && probabilities.Count == 3 && probabilities[2] >= 0.5))
                {
                    return ActionRiskScore.IrreversibleOrExternalEffect;
                }
                else if (score == 2 || score == 1)
                {
                    return ActionRiskScore.ReversibleEdit;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate action risk via Jev. Defaulting to ReversibleEdit.");
            return ActionRiskScore.ReversibleEdit;
        }

        return ActionRiskScore.Harmless;
    }

    private static Dictionary<string, string> BuildCandidateChoices(string goal, IReadOnlyList<AccessibilityElement> elements)
    {
        var choices = new Dictionary<string, string>();

        // 0. App Launch & URL candidates
        var appCandidates = ExtractAppLaunchCandidates(goal);
        foreach (var app in appCandidates)
        {
            choices[$"open_app:{app}"] = $"Launch or switch to application \"{app}\"";
        }

        var urlCandidates = UrlLauncherValidator.ExtractWebUrls(goal);
        foreach (var url in urlCandidates)
        {
            choices[$"open_url:{url}"] = $"Open web URL \"{url}\" in browser";
        }

        // System Audio & Volume candidates
        bool isVolumeGoal = IsVolumeGoal(goal);
        if (isVolumeGoal)
        {
            if (TryExtractVolumeTarget(goal, out int targetVol))
            {
                choices[$"volume:set:{targetVol}"] = $"Set system audio volume directly to {targetVol}%";
            }

            choices["volume:check"] = "Check current system audio volume level";
            choices["volume:up"] = "Increase system audio volume";
            choices["volume:down"] = "Decrease system audio volume";
            choices["volume:mute"] = "Toggle system audio mute";
            choices["open_app:sndvol"] = "Open Windows Volume Mixer";
            choices["open_app:ms-settings:sound"] = "Open Windows Sound Settings";
        }

        // Media playback candidates
        bool isMediaGoal = IsMediaGoal(goal);
        if (isMediaGoal)
        {
            choices["media:play_pause"] = "Toggle media playback (play/pause)";
            choices["media:next"] = "Skip to next media track or song";
            choices["media:previous"] = "Return to previous media track or song";
        }

        // System workstation control candidates
        bool isSystemControlGoal = IsSystemControlGoal(goal);
        if (isSystemControlGoal)
        {
            if (Regex.IsMatch(goal, @"\block\b", RegexOptions.IgnoreCase))
            {
                choices["system:lock"] = "Lock the Windows workstation screen";
            }
            if (Regex.IsMatch(goal, @"\b(?:desktop|minimize|hide)\b", RegexOptions.IgnoreCase))
            {
                choices["system:show_desktop"] = "Show Windows desktop (minimize all windows)";
            }
            if (Regex.IsMatch(goal, @"\b(?:screenshot|snip|capture)\b", RegexOptions.IgnoreCase))
            {
                choices["system:screenshot"] = "Take a screenshot using Windows Snipping Tool";
            }
        }

        bool isPureLaunch = IsPureLaunchGoal(goal);
        bool isSystemGoal = isVolumeGoal || isMediaGoal || isSystemControlGoal || isPureLaunch;

        // Only populate foreground UI elements if the task is NOT a pure system command (app launch or audio)
        if (!isSystemGoal)
        {
            // Extract search/literal candidate phrases from user goal
            var textCandidates = ExtractCandidatePhrases(goal);

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

                if (IsTypeable(el.Role) && textCandidates.Count > 0)
                {
                    foreach (var textCandidate in textCandidates.Take(3))
                    {
                        // If element already contains this exact text, avoid looping!
                        if (!string.IsNullOrEmpty(el.Value) && el.Value.Contains(textCandidate, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var keyEnter = $"type_and_enter:{el.Id}:{textCandidate}";
                        var descEnter = $"Type \"{textCandidate}\" into {el.DisplayRole} \"{el.DisplayLabel}\" and press Enter";
                        choices[keyEnter] = descEnter;

                        var key = $"type:{el.Id}:{textCandidate}";
                        var desc = $"Type \"{textCandidate}\" into {el.DisplayRole} \"{el.DisplayLabel}\"";
                        choices[key] = desc;
                    }
                }
            }

            // Standard actions for UI navigation
            choices["press:enter"] = "Press Enter/Return key";
            choices["press:space"] = "Press Spacebar to play/pause or select";
            choices["press:media_play"] = "Press Media Play key to toggle playback";
            choices["press:tab"] = "Press Tab key to advance focus";
            choices["press:escape"] = "Press Escape key to dismiss dialog/menu";
            choices["scroll:down"] = "Scroll down to reveal more controls";
            choices["scroll:up"] = "Scroll up";
        }

        choices["wait"] = "Wait 1 second for UI to update";
        choices["done"] = "Task is completely finished";
        choices["ask_user"] = "Need human guidance or clarification";

        return choices;
    }

    private static AgentDecision ParseActionDecision(string key, double confidence, IReadOnlyList<AccessibilityElement> elements)
    {
        if (key.StartsWith("open_app:", StringComparison.OrdinalIgnoreCase))
        {
            var appName = key[9..].Trim();
            return new AgentDecision
            {
                Operation = AgentOperation.OpenApp,
                TargetId = appName,
                TargetLabel = appName,
                Confidence = confidence,
                Reason = $"Launch application '{appName}'"
            };
        }

        if (key.StartsWith("open_url:", StringComparison.OrdinalIgnoreCase))
        {
            var url = key[9..].Trim();
            return new AgentDecision
            {
                Operation = AgentOperation.OpenUrl,
                TargetId = url,
                TargetLabel = url,
                TextValue = url,
                Confidence = confidence,
                Reason = $"Open web URL '{url}'"
            };
        }

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

        if (key.StartsWith("type_and_enter:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = key.Split(':', 3);
            var elementId = parts.Length > 1 ? parts[1] : string.Empty;
            var text = parts.Length > 2 ? parts[2] : string.Empty;
            var el = elements.FirstOrDefault(e => e.Id == elementId);

            return new AgentDecision
            {
                Operation = AgentOperation.TypeAndEnter,
                TargetId = elementId,
                TargetLabel = el?.DisplayLabel,
                TextValue = text,
                Confidence = confidence,
                Reason = $"Type '{text}' into {elementId} and press Enter"
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

        if (key.StartsWith("volume:set:", StringComparison.OrdinalIgnoreCase))
        {
            var pct = key[11..].Trim();
            return new AgentDecision
            {
                Operation = AgentOperation.VolumeSet,
                TextValue = pct,
                Confidence = confidence,
                Reason = $"Set system audio volume to {pct}%"
            };
        }

        return key.ToLowerInvariant() switch
        {
            "press:enter" => new AgentDecision { Operation = AgentOperation.PressReturn, Confidence = confidence },
            "press:space" => new AgentDecision { Operation = AgentOperation.PressSpace, Confidence = confidence },
            "press:media_play" => new AgentDecision { Operation = AgentOperation.PressMediaPlay, Confidence = confidence },
            "volume:check" => new AgentDecision { Operation = AgentOperation.CheckVolume, Confidence = confidence, Reason = "Check system audio volume" },
            "volume:up" => new AgentDecision { Operation = AgentOperation.VolumeUp, Confidence = confidence, Reason = "Increase system volume" },
            "volume:down" => new AgentDecision { Operation = AgentOperation.VolumeDown, Confidence = confidence, Reason = "Decrease system volume" },
            "volume:mute" => new AgentDecision { Operation = AgentOperation.VolumeMute, Confidence = confidence, Reason = "Toggle system audio mute" },
            "media:play_pause" => new AgentDecision { Operation = AgentOperation.MediaPlayPause, Confidence = confidence, Reason = "Toggle media playback" },
            "media:next" => new AgentDecision { Operation = AgentOperation.MediaNext, Confidence = confidence, Reason = "Skip to next media track" },
            "media:previous" => new AgentDecision { Operation = AgentOperation.MediaPrevious, Confidence = confidence, Reason = "Return to previous media track" },
            "system:lock" => new AgentDecision { Operation = AgentOperation.LockWorkstation, Confidence = confidence, Reason = "Lock workstation screen" },
            "system:show_desktop" => new AgentDecision { Operation = AgentOperation.ShowDesktop, Confidence = confidence, Reason = "Show Windows desktop" },
            "system:screenshot" => new AgentDecision { Operation = AgentOperation.TakeScreenshot, Confidence = confidence, Reason = "Take screenshot" },
            "press:tab" => new AgentDecision { Operation = AgentOperation.PressTab, Confidence = confidence },
            "press:escape" => new AgentDecision { Operation = AgentOperation.PressEscape, Confidence = confidence },
            "scroll:down" => new AgentDecision { Operation = AgentOperation.ScrollDown, Confidence = confidence },
            "scroll:up" => new AgentDecision { Operation = AgentOperation.ScrollUp, Confidence = confidence },
            "wait" => new AgentDecision { Operation = AgentOperation.Wait, Confidence = confidence },
            "done" => new AgentDecision { Operation = AgentOperation.Done, Confidence = confidence },
            _ => new AgentDecision { Operation = AgentOperation.AskUser, Confidence = confidence }
        };
    }

    private static bool IsConversationalOrGreeting(string goal)
    {
        if (string.IsNullOrWhiteSpace(goal)) return false;
        var cleaned = goal.Trim().ToLowerInvariant().TrimEnd('.', '!', '?', ' ');
        string[] greetings = ["hi", "hello", "hey", "hola", "yo", "bark", "meow", "test", "who are you", "what can you do", "help"];
        return greetings.Contains(cleaned);
    }

    public static bool IsVolumeGoal(string goal)
    {
        if (string.IsNullOrWhiteSpace(goal)) return false;
        return Regex.IsMatch(goal, @"\b(?:volume|sound|audio|mute|unmute|quieter|louder)\b", RegexOptions.IgnoreCase);
    }

    public static bool TryExtractVolumeTarget(string goal, out int volumePercent)
    {
        volumePercent = 0;
        if (string.IsNullOrWhiteSpace(goal)) return false;

        // Keywords
        if (Regex.IsMatch(goal, @"\b(?:max|maximum|full)\s+(?:volume|sound|audio)\b|\b(?:volume|sound|audio)\s+(?:to\s+)?(?:max|maximum|full)\b", RegexOptions.IgnoreCase))
        {
            volumePercent = 100;
            return true;
        }

        if (Regex.IsMatch(goal, @"\bhalf\s+(?:volume|sound|audio)\b|\b(?:volume|sound|audio)\s+(?:to\s+)?half\b", RegexOptions.IgnoreCase))
        {
            volumePercent = 50;
            return true;
        }

        if (Regex.IsMatch(goal, @"\bquarter\s+(?:volume|sound|audio)\b|\b(?:volume|sound|audio)\s+(?:to\s+)?quarter\b", RegexOptions.IgnoreCase))
        {
            volumePercent = 25;
            return true;
        }

        if (Regex.IsMatch(goal, @"\bzero\s+(?:volume|sound|audio)\b|\b(?:volume|sound|audio)\s+(?:to\s+)?zero\b", RegexOptions.IgnoreCase))
        {
            volumePercent = 0;
            return true;
        }

        // Direct number 0-100: "set volume to 69", "volume 50", "set volume to 80%", "turn volume to 69"
        var match = Regex.Match(goal, @"\b(\d{1,3})\s*(?:%|\s*percent)?\b");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var num) && num >= 0 && num <= 100)
        {
            volumePercent = num;
            return true;
        }

        return false;
    }

    public static bool IsMediaGoal(string goal)
    {
        if (string.IsNullOrWhiteSpace(goal)) return false;
        return Regex.IsMatch(goal, @"\b(?:play|pause|resume|play\s*pause|next\s+track|next\s+song|skip\s+song|skip\s+track|previous\s+song|prev\s+song|previous\s+track|stop\s+music)\b", RegexOptions.IgnoreCase);
    }

    public static bool IsSystemControlGoal(string goal)
    {
        if (string.IsNullOrWhiteSpace(goal)) return false;
        return Regex.IsMatch(goal, @"\b(?:lock(?:\s+(?:screen|pc|laptop|workstation|computer))?|show\s+desktop|minimize\s+all|hide\s+all\s+windows|take\s+screenshot|screenshot|snip|screen\s+snip|capture\s+screen)\b", RegexOptions.IgnoreCase);
    }

    public static bool IsPureLaunchGoal(string goal)
    {
        if (string.IsNullOrWhiteSpace(goal)) return false;
        var trimmed = Regex.Replace(goal.Trim(), @"^(?:please\s+|can\s+you\s+|kindly\s+)", "", RegexOptions.IgnoreCase).TrimEnd('.', '!', '?', ' ');

        // If it contains conjunctions indicating compound steps, it's not a pure launch
        if (Regex.IsMatch(trimmed, @"\s+(?:and|then|after\s+that|followed\s+by)\s+", RegexOptions.IgnoreCase))
        {
            return false;
        }

        if (Regex.IsMatch(trimmed, @"^(?:open|launch|start|run|switch\s+to|go\s+to|focus)\s+(?:the\s+app\s+)?([a-zA-Z0-9\-_ ]+)$", RegexOptions.IgnoreCase))
        {
            return true;
        }

        return SystemAliases.IsKnownAlias(trimmed);
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

    public static List<string> ExtractCandidatePhrases(string goal)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Quoted text: "Adele", 'Hello World'
        foreach (Match m in Regex.Matches(goal, @"[""']([^""']+)[""']"))
        {
            var val = m.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(val)) candidates.Add(val);
        }

        // 2. Action verbs with target hints: "write/type/enter/insert/put <text> in/into/there/..."
        var writeMatch = Regex.Match(goal, @"(?:write|type|enter|insert|put)\s+(?:the\s+text\s+)?(?:[""']?)(.+?)(?:[""']?)(?:\s+(?:in|into|there|here|on|to)\b|$|\.)", RegexOptions.IgnoreCase);
        if (writeMatch.Success)
        {
            var val = writeMatch.Groups[1].Value.Trim();
            val = Regex.Replace(val, @"\s+(?:in|into|to|on)\s+(?:notepad|document|file|editor|app|browser|search|bar|box).*$", "", RegexOptions.IgnoreCase).Trim();
            if (!string.IsNullOrEmpty(val) && !val.Equals("there", StringComparison.OrdinalIgnoreCase))
                candidates.Add(val);
        }

        // 3. Search queries: "search/look up/find/google/query [for/about/on] <text>"
        var searchMatches = Regex.Matches(
            goal,
            @"(?:search|look\s+up|find|google|query)(?:\s+(?:for|about|on|regarding|the\s+web\s+for))?\s+(?:[""']?)(.+?)(?:[""']?)(?:\s+(?:on|in|using|with)\s+[a-zA-Z0-9_\-]+|\.|$|\band\b)",
            RegexOptions.IgnoreCase);
        foreach (Match m in searchMatches)
        {
            var val = m.Groups[1].Value.Trim();
            val = Regex.Replace(val, @"^(?:for|about|on)\s+", "", RegexOptions.IgnoreCase).Trim();
            if (!string.IsNullOrEmpty(val))
                candidates.Add(val);
        }

        // 4. Play / Listen / Stream queries: "play/listen to [any song of/by/music by] <text>"
        var playMatches = Regex.Matches(
            goal,
            @"(?:play|listen\s+to|stream)(?:\s+(?:any\s+song\s+(?:of|by)|songs?\s+(?:of|by)|music\s+(?:of|by)|tracks?\s+(?:of|by)))?\s+(?:[""']?)(.+?)(?:[""']?)(?:\s+(?:on|in|using|with)\s+[a-zA-Z0-9_\-]+|\.|$|\band\b)",
            RegexOptions.IgnoreCase);
        foreach (Match m in playMatches)
        {
            var val = m.Groups[1].Value.Trim();
            val = Regex.Replace(val, @"^(?:any\s+song\s+(?:of|by)|songs?\s+(?:of|by)|music\s+(?:of|by)|track\s+(?:of|by))\s+", "", RegexOptions.IgnoreCase).Trim();
            if (!string.IsNullOrEmpty(val))
                candidates.Add(val);
        }

        // 5. Calculations: "calculate/compute <expression>"
        var calcMatch = Regex.Match(goal, @"(?:calculate|calc|compute)\s+(.+)$", RegexOptions.IgnoreCase);
        if (calcMatch.Success)
        {
            var val = calcMatch.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(val)) candidates.Add(val);
        }

        // 6. Fallback: If no candidate extracted yet, see if goal is a direct short phrase
        if (candidates.Count == 0 && !goal.StartsWith("click", StringComparison.OrdinalIgnoreCase) && !goal.StartsWith("scroll", StringComparison.OrdinalIgnoreCase))
        {
            var cleaned = Regex.Replace(goal, @"^(?:please\s+|can\s+you\s+|i\s+want\s+to\s+)", "", RegexOptions.IgnoreCase).Trim();
            if (cleaned.Length > 0 && cleaned.Length <= 40)
            {
                candidates.Add(cleaned);
            }
        }

        return candidates.ToList();
    }

    public static List<string> ExtractAppLaunchCandidates(string goal)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(goal)) return candidates.ToList();

        // Extract app name dynamically from user goal intent:
        // e.g. "open notepad", "launch vlc", "start calculator", "switch to discord", "go to chrome"
        var match = Regex.Match(
            goal,
            @"(?:open|launch|start|run|switch\s+to|go\s+to|focus)\s+(?:the\s+app\s+)?([a-zA-Z0-9\-_ ]+?)(?:\s+(?:and|to|then|in|with)\b|$|\.)",
            RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var app = match.Groups[1].Value.Trim();
            string[] stopWords = ["menu", "tab", "link", "window", "dialog", "document", "file", "page", "browser", "app", "application"];
            if (!stopWords.Contains(app, StringComparer.OrdinalIgnoreCase) && app.Length > 0)
            {
                candidates.Add(app);
            }
        }

        // 2. Direct shortcut or alias match
        var cleaned = Regex.Replace(goal.Trim(), @"^(?:please\s+|can\s+you\s+|kindly\s+)", "", RegexOptions.IgnoreCase).TrimEnd('.', '!', '?', ' ');
        if (SystemAliases.IsKnownAlias(cleaned))
        {
            candidates.Add(cleaned);
        }

        return candidates.ToList();
    }

    private static string Truncate(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars) return text;
        return text[..maxChars];
    }
}
