using System.Drawing;

namespace HolyHand.Core.Models;

/// <summary>
/// Platform-independent representation of a readable UI control or OCR detection on screen.
/// Directly mirrors upstream AccessibilityElement model with Windows UIA role mapping.
/// </summary>
public record AccessibilityElement
{
    public required string Id { get; init; }
    public required string Role { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
    public bool Focused { get; init; }
    public Rectangle Frame { get; init; }
    public string Source { get; init; } = "accessibility"; // "accessibility" or "ocr"
    public IReadOnlyList<string> Actions { get; init; } = Array.Empty<string>();

    public string DisplayRole => Role.StartsWith("AX", StringComparison.OrdinalIgnoreCase) 
        ? Role[2..] 
        : Role;

    public string DisplayLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Label))
                return Label;
            if (!string.IsNullOrWhiteSpace(Value))
                return Value;
            return string.Empty;
        }
    }

    public bool IsOutcomeEvidence =>
        Role.Equals("Text", StringComparison.OrdinalIgnoreCase) ||
        Role.Equals("StaticText", StringComparison.OrdinalIgnoreCase) ||
        Role.Equals("AXStaticText", StringComparison.OrdinalIgnoreCase) ||
        Role.Equals("StatusBar", StringComparison.OrdinalIgnoreCase);

    public string CompactDescription(int maxChars = 160)
    {
        var text = !string.IsNullOrWhiteSpace(DisplayLabel) ? DisplayLabel : DisplayRole;
        if (text.Length > maxChars)
        {
            return text[..maxChars] + "...";
        }
        return text;
    }
}
