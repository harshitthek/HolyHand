using System.Text.RegularExpressions;

namespace HolyHand.Core.ScreenReading;

public static class SecretSanitizer
{
    // Matches credit card numbers: 13-16 digits with optional spaces or dashes
    private static readonly Regex CardRegex = new(
        @"\b(?:\d{4}[ -]?\d{4}[ -]?\d{4}[ -]?\d{1,4}|\d{13,16})\b",
        RegexOptions.Compiled);

    // Matches API keys and tokens: vck_*, sk-*, ghp_*, eyJ* (JWT)
    private static readonly Regex ApiKeyRegex = new(
        @"\b(?:vck_[a-zA-Z0-9_-]{10,}|sk-[a-zA-Z0-9_-]{20,}|ghp_[a-zA-Z0-9]{25,}|eyJ[a-zA-Z0-9_-]{20,}\.[a-zA-Z0-9_-]{20,}\.[a-zA-Z0-9_-]{10,})\b",
        RegexOptions.Compiled);

    // Matches Bearer authorization headers
    private static readonly Regex BearerRegex = new(
        @"(Bearer\s+)[a-zA-Z0-9_\-\.]{15,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Sanitize(string? text, bool isPassword = false)
    {
        if (isPassword)
            return "[PASSWORD]";

        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var sanitized = CardRegex.Replace(text, "[REDACTED_CARD]");
        sanitized = ApiKeyRegex.Replace(sanitized, "[REDACTED_KEY]");
        sanitized = BearerRegex.Replace(sanitized, "$1[REDACTED]");

        return sanitized;
    }
}
