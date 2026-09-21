using HolyHand.Core.Interfaces;
using HolyHand.Core.Models;

namespace HolyHand.Cli;

public class ConsoleConfirmationPrompt : IConfirmationPrompt
{
    private readonly bool? _autoApprove;

    public ConsoleConfirmationPrompt(bool? autoApprove = null)
    {
        _autoApprove = autoApprove;
    }

    public Task<bool> RequestConfirmationAsync(
        AgentDecision decision,
        AccessibilityElement? target,
        AppTarget appTarget,
        string reason,
        CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("                       SAFETY CONFIRMATION REQUIRED                             ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        Console.WriteLine($"Action:     {decision.Operation}");
        Console.WriteLine($"Target:     {target?.DisplayLabel ?? decision.TargetLabel ?? decision.TargetId} ({target?.DisplayRole ?? "Control"})");
        if (!string.IsNullOrEmpty(decision.TextValue))
        {
            Console.WriteLine($"Value:      \"{decision.TextValue}\"");
        }
        Console.WriteLine($"App:        {appTarget.ProcessName} — \"{appTarget.WindowTitle}\"");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Reason:     {reason}");
        Console.ResetColor();
        Console.WriteLine("--------------------------------------------------------------------------------");

        if (_autoApprove.HasValue)
        {
            var choice = _autoApprove.Value ? "Y (auto)" : "N (auto)";
            Console.WriteLine($"Automated response: {choice}\n");
            return Task.FromResult(_autoApprove.Value);
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Do you approve executing this action? [Y/N] (Default: N): ");
        Console.ResetColor();

        try
        {
            var input = Console.ReadLine()?.Trim();
            bool approved = string.Equals(input, "y", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(input, "yes", StringComparison.OrdinalIgnoreCase);

            Console.WriteLine(approved ? "Action APPROVED by human.\n" : "Action REJECTED by human.\n");
            return Task.FromResult(approved);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
