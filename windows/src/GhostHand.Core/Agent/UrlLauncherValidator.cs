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

    private static readonly Dictionary<string, string> KnownWebsites = new(StringComparer.OrdinalIgnoreCase)
    {
        ["youtube"] = "https://www.youtube.com",
        ["google"] = "https://www.google.com",
        ["github"] = "https://www.github.com",
        ["reddit"] = "https://www.reddit.com",
        ["twitter"] = "https://x.com",
        ["x"] = "https://x.com",
        ["wikipedia"] = "https://www.wikipedia.org",
        ["netflix"] = "https://www.netflix.com",
        ["amazon"] = "https://www.amazon.com",
        ["gmail"] = "https://mail.google.com",
        ["linkedin"] = "https://www.linkedin.com"
    };

    public static IReadOnlyList<Uri> ExtractWebUrls(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return Array.Empty<Uri>();

        var list = new List<Uri>();

        // 1. Explicit http/https URLs in the text
        var matches = UrlRegex.Matches(prompt);
        foreach (Match match in matches)
        {
            if (IsValidWebUrl(match.Value, out var uri) && uri != null)
            {
                list.Add(uri);
            }
        }

        if (list.Count > 0)
            return list;

        // 2. Synthesize web search intent (e.g. "search for Adele on youtube", "search for X on google")
        var searchOnMatch = Regex.Match(prompt, @"(?:search|look)\s+for\s+(.+?)\s+(?:on|in)\s+(google|youtube|bing|reddit|wikipedia)", RegexOptions.IgnoreCase);
        if (searchOnMatch.Success)
        {
            var query = searchOnMatch.Groups[1].Value.Trim().Trim('"', '\'');
            var engine = searchOnMatch.Groups[2].Value.Trim().ToLowerInvariant();
            var searchUrl = engine switch
            {
                "youtube" => $"https://www.youtube.com/results?search_query={Uri.EscapeDataString(query)}",
                "bing" => $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}",
                "reddit" => $"https://www.reddit.com/search/?q={Uri.EscapeDataString(query)}",
                "wikipedia" => $"https://en.wikipedia.org/wiki/Special:Search?search={Uri.EscapeDataString(query)}",
                _ => $"https://www.google.com/search?q={Uri.EscapeDataString(query)}"
            };
            if (IsValidWebUrl(searchUrl, out var uri) && uri != null)
            {
                list.Add(uri);
                return list;
            }
        }

        // 3. Direct engine shortcuts (e.g. "google <query>", "search google for <query>")
        var googleMatch = Regex.Match(prompt, @"^(?:please\s+)?(?:google|search\s+google\s+for)\s+(.+)$", RegexOptions.IgnoreCase);
        if (googleMatch.Success)
        {
            var query = googleMatch.Groups[1].Value.Trim().Trim('"', '\'');
            if (!string.IsNullOrEmpty(query))
            {
                var searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
                if (IsValidWebUrl(searchUrl, out var uri) && uri != null)
                {
                    list.Add(uri);
                    return list;
                }
            }
        }

        var youtubeMatch = Regex.Match(prompt, @"^(?:please\s+)?(?:youtube|search\s+youtube\s+for)\s+(.+)$", RegexOptions.IgnoreCase);
        if (youtubeMatch.Success)
        {
            var query = youtubeMatch.Groups[1].Value.Trim().Trim('"', '\'');
            if (!string.IsNullOrEmpty(query))
            {
                var searchUrl = $"https://www.youtube.com/results?search_query={Uri.EscapeDataString(query)}";
                if (IsValidWebUrl(searchUrl, out var uri) && uri != null)
                {
                    list.Add(uri);
                    return list;
                }
            }
        }

        // 4. Known website destinations (e.g. "open youtube", "open github")
        var openSiteMatch = Regex.Match(prompt, @"^(?:please\s+)?(?:open|go\s+to|visit)\s+([a-zA-Z0-9\-_]+)(?:\.com|\.org|\.net)?(?:\s.*)?$", RegexOptions.IgnoreCase);
        if (openSiteMatch.Success)
        {
            var siteKey = openSiteMatch.Groups[1].Value.Trim().ToLowerInvariant();
            if (KnownWebsites.TryGetValue(siteKey, out var siteUrl))
            {
                if (IsValidWebUrl(siteUrl, out var uri) && uri != null)
                {
                    list.Add(uri);
                    return list;
                }
            }
        }

        return list;
    }
}
