using HolyHand.Core.Interfaces;
using HolyHand.Core.Models;
using Microsoft.Extensions.Logging;

namespace HolyHand.Core.Agent;

public enum AgentRunStatus
{
    Completed,
    NeedsHumanInput,
    Stalled,
    MaxStepsReached,
    Cancelled,
    Failed
}

public record AgentRunResult
{
    public required AgentRunStatus Status { get; init; }
    public int StepsCompleted { get; init; }
    public IReadOnlyList<string> ActionHistory { get; init; } = Array.Empty<string>();
    public string? Message { get; init; }

    public static AgentRunResult Completed(int steps, IReadOnlyList<string> history) => new()
    {
        Status = AgentRunStatus.Completed,
        StepsCompleted = steps,
        ActionHistory = history,
        Message = "Goal successfully achieved."
    };

    public static AgentRunResult NeedsHumanInput(int steps, IReadOnlyList<string> history, string? reason) => new()
    {
        Status = AgentRunStatus.NeedsHumanInput,
        StepsCompleted = steps,
        ActionHistory = history,
        Message = reason ?? "Human input required."
    };

    public static AgentRunResult Stalled(int steps, IReadOnlyList<string> history, string? reason) => new()
    {
        Status = AgentRunStatus.Stalled,
        StepsCompleted = steps,
        ActionHistory = history,
        Message = reason ?? "Loop guard tripped: screen state unchanged."
    };

    public static AgentRunResult MaxStepsReached(int steps, IReadOnlyList<string> history) => new()
    {
        Status = AgentRunStatus.MaxStepsReached,
        StepsCompleted = steps,
        ActionHistory = history,
        Message = $"Reached maximum step limit ({steps})."
    };

    public static AgentRunResult Failed(int steps, IReadOnlyList<string> history, string error) => new()
    {
        Status = AgentRunStatus.Failed,
        StepsCompleted = steps,
        ActionHistory = history,
        Message = error
    };

    public static AgentRunResult Cancelled(int steps, IReadOnlyList<string> history) => new()
    {
        Status = AgentRunStatus.Cancelled,
        StepsCompleted = steps,
        ActionHistory = history,
        Message = "Run was cancelled."
    };
}

public class AgentLoop
{
    private readonly IScreenReader _screenReader;
    private readonly IDecisionModel _decisionModel;
    private readonly IActionExecutor _actionExecutor;
    private readonly AgentLoopOptions _options;
    private readonly ILogger<AgentLoop> _logger;

    public event Action<string>? StatusChanged;
    public event Action<int, AgentDecision, ActionResult>? StepCompleted;

    public AgentLoop(
        IScreenReader screenReader,
        IDecisionModel decisionModel,
        IActionExecutor actionExecutor,
        AgentLoopOptions options,
        ILogger<AgentLoop> logger)
    {
        _screenReader = screenReader;
        _decisionModel = decisionModel;
        _actionExecutor = actionExecutor;
        _options = options;
        _logger = logger;
    }

    public async Task<AgentRunResult> RunAsync(
        string goal,
        AppTarget target,
        CancellationToken cancellationToken = default)
    {
        var history = new List<string>();
        var loopGuard = new LoopGuard(_options.MaxConsecutiveStalls);
        int step = 0;

        _logger.LogInformation("Starting AgentLoop for goal '{Goal}' on target '{Target}' (DryRun: {DryRun})",
            goal, target.ProcessName, _options.DryRun);

        try
        {
            while (step < _options.MaxSteps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                step++;

                // 1. Observe screen elements
                NotifyStatus($"Step {step}/{_options.MaxSteps}: Reading screen...");
                var elements = await _screenReader.ReadElementsAsync(target, cancellationToken);

                // 2. Loop guard stall check
                if (loopGuard.RecordObservation(elements))
                {
                    _logger.LogWarning("Stall detected: {Count} identical consecutive observations.", loopGuard.ConsecutiveStalls);
                    NotifyStatus("Stall detected: screen state did not change.");
                    return AgentRunResult.Stalled(step, history, "Loop guard tripped: screen state did not change across actions.");
                }

                // 3. Jev Call A: Next action & Goal completion check
                NotifyStatus($"Step {step}/{_options.MaxSteps}: Choosing next action...");
                var decision = await _decisionModel.DecideNextActionAsync(goal, target, elements, history, cancellationToken);

                // 4. Check if decision is Done
                if (decision.Operation == AgentOperation.Done)
                {
                    NotifyStatus("Verifying goal completion...");
                    var verified = await _decisionModel.VerifyCompletionAsync(goal, target, elements, history, cancellationToken);
                    if (verified)
                    {
                        NotifyStatus("Goal successfully completed!");
                        return AgentRunResult.Completed(step, history);
                    }

                    _logger.LogInformation("Done operation verification was inconclusive. Continuing loop.");
                }

                // 5. Check if decision is AskUser / Low confidence
                if (decision.Operation == AgentOperation.AskUser)
                {
                    NotifyStatus($"Guidance needed: {decision.Reason}");
                    return AgentRunResult.NeedsHumanInput(step, history, decision.Reason);
                }

                // 6. Locate target element
                var targetElement = !string.IsNullOrEmpty(decision.TargetId)
                    ? elements.FirstOrDefault(e => e.Id == decision.TargetId)
                    : null;

                // 7. Execute action (Dry-run or live)
                var actionLabel = targetElement != null ? $"'{targetElement.DisplayLabel}'" : decision.TargetId;
                NotifyStatus($"Step {step}/{_options.MaxSteps}: {decision.Operation} on {actionLabel}");

                var result = await _actionExecutor.ExecuteAsync(decision, targetElement, cancellationToken);

                var historyEntry = $"{decision.Operation}:{decision.TargetId} ({decision.TargetLabel}) -> {(result.Success ? "ok" : result.Error)}";
                history.Add(historyEntry);

                StepCompleted?.Invoke(step, decision, result);

                if (!result.Success)
                {
                    var err = result.ErrorMessage ?? result.Error ?? "Action execution failed.";
                    _logger.LogWarning("Action execution failed at step {Step}: {Error}", step, err);
                    return AgentRunResult.Failed(step, history, err);
                }
            }

            NotifyStatus($"Max steps ({_options.MaxSteps}) reached.");
            return AgentRunResult.MaxStepsReached(step, history);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("AgentLoop was cancelled.");
            NotifyStatus("Run cancelled by user.");
            return AgentRunResult.Cancelled(step, history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AgentLoop terminated unexpectedly.");
            NotifyStatus($"Error: {ex.Message}");
            return AgentRunResult.Failed(step, history, ex.Message);
        }
    }

    private void NotifyStatus(string message)
    {
        _logger.LogDebug("[AgentLoop] {Message}", message);
        StatusChanged?.Invoke(message);
    }
}
