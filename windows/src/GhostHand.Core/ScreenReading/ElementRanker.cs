using HolyHand.Core.Models;

namespace HolyHand.Core.ScreenReading;

public static class ElementRanker
{
    private static readonly HashSet<string> InteractiveRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Button", "MenuItem", "TabItem", "Hyperlink", "CheckBox",
        "RadioButton", "ComboBox", "ListItem", "Edit", "Document", "SplitButton"
    };

    public static IReadOnlyList<AccessibilityElement> RankAndFilter(
        IEnumerable<AccessibilityElement> elements,
        ScreenReaderOptions options)
    {
        var filtered = elements
            .Where(e => !options.FilterOffscreen || IsOnScreen(e))
            .Where(e => !options.FilterDisabled || e.Enabled)
            .Take(options.MaxNodes)
            .ToList();

        // Sort by priority:
        // 1. Focused element first
        // 2. Interactive elements
        // 3. Elements with meaningful labels/values
        // 4. Outcome evidence / status elements
        // 5. Visual order (top-to-bottom, then left-to-right)
        var sorted = filtered
            .OrderByDescending(e => e.Focused)
            .ThenByDescending(e => IsInteractive(e.Role))
            .ThenByDescending(e => !string.IsNullOrWhiteSpace(e.DisplayLabel))
            .ThenByDescending(e => e.IsOutcomeEvidence)
            .ThenBy(e => e.Frame.Y)
            .ThenBy(e => e.Frame.X)
            .ToList();

        // Cap to MaxCandidates while preserving top elements
        var candidateList = sorted.Take(options.MaxCandidates).ToList();

        // Assign stable sequential IDs: e1, e2, e3...
        var result = new List<AccessibilityElement>(candidateList.Count);
        for (int i = 0; i < candidateList.Count; i++)
        {
            var el = candidateList[i];
            result.Add(el with
            {
                Id = $"e{i + 1}"
            });
        }

        return result;
    }

    public static bool IsInteractive(string role) =>
        InteractiveRoles.Contains(role) ||
        (role.StartsWith("AX", StringComparison.OrdinalIgnoreCase) && InteractiveRoles.Contains(role[2..]));

    private static bool IsOnScreen(AccessibilityElement element)
    {
        // Must have non-zero dimension and non-negative coordinates
        return element.Frame.Width > 0 && element.Frame.Height > 0;
    }
}
