using System.Text.RegularExpressions;

namespace HolyHand.Core.Agent;

public static class UrlLauncherValidator
{
    private static readonly Regex UrlRegex = new(
        @"https?://[^\s""'<>]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsValidWebUrl(string? rawUrl, out Uri? validatedUri)
    {
        validatedUri = null;
        if (string.IsNullOrWhiteSpace(rawUrl))
            return false;

        // Strip trailing punctuation like period, comma, paren
        var trimmed = rawUrl.Trim().TrimEnd('.', ',', ')', ']', ';');

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return false;

        // Strictly enforce http or https
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        // Must have a valid host
        if (string.IsNullOrWhiteSpace(uri.Host) || uri.Host.Length < 3)
            return false;

        // Disallow dangerous embedded user info
        if (!string.IsNullOrEmpty(uri.UserInfo))
            return false;

        validatedUri = uri;
        return true;
    }

    public static IReadOnlyList<Uri> ExtractWebUrls(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return Array.Empty<Uri>();

        var list = new List<Uri>();
        var matches = UrlRegex.Matches(prompt);
        foreach (Match match in matches)
        {
            if (IsValidWebUrl(match.Value, out var uri) && uri != null)
            {
                list.Add(uri);
            }
        }

        return list;
    }
}
