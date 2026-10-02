using FluentAssertions;
using HolyHand.Core.Agent;
using Xunit;

namespace HolyHand.Tests.Agent;

public class UrlLauncherValidatorTests
{
    [Theory]
    [InlineData("https://www.google.com")]
    [InlineData("http://example.com")]
    [InlineData("https://github.com/harshitthek/HolyHand")]
    [InlineData("https://en.wikipedia.org/wiki/Main_Page?query=test#section")]
    [InlineData("https://subdomain.domain.co.uk:8080/path/to/resource?arg=val")]
    public void IsValidWebUrl_ValidHttpAndHttps_ReturnsTrue(string url)
    {
        bool isValid = UrlLauncherValidator.IsValidWebUrl(url, out var uri);

        isValid.Should().BeTrue();
        uri.Should().NotBeNull();
        uri!.Scheme.Should().Match(s => s == "http" || s == "https");
    }

    [Theory]
    [InlineData("ftp://ftp.example.com/file.txt")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<h1>Hello</h1>")]
    [InlineData("about:blank")]
    [InlineData("tel:+123456789")]
    [InlineData("mailto:user@example.com")]
    public void IsValidWebUrl_DangerousOrUnsupportedSchemes_ReturnsFalse(string url)
    {
        bool isValid = UrlLauncherValidator.IsValidWebUrl(url, out var uri);

        isValid.Should().BeFalse();
        uri.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("http://")]
    [InlineData("https://")]
    [InlineData("http://ab")] // Host too short (<3 chars)
    public void IsValidWebUrl_MalformedOrEmpty_ReturnsFalse(string? url)
    {
        bool isValid = UrlLauncherValidator.IsValidWebUrl(url, out var uri);

        isValid.Should().BeFalse();
        uri.Should().BeNull();
    }

    [Fact]
    public void IsValidWebUrl_UrlWithEmbeddedCredentials_ReturnsFalse()
    {
        string maliciousUrl = "https://admin:supersecret@malicious.com/steal";

        bool isValid = UrlLauncherValidator.IsValidWebUrl(maliciousUrl, out var uri);

        isValid.Should().BeFalse();
        uri.Should().BeNull();
    }

    [Theory]
    [InlineData("https://github.com/harshitthek/HolyHand.", "https://github.com/harshitthek/HolyHand")]
    [InlineData("https://google.com,", "https://google.com/")]
    [InlineData("https://wikipedia.org)", "https://wikipedia.org/")]
    [InlineData("https://example.com];", "https://example.com/")]
    public void IsValidWebUrl_TrailingPunctuation_StripsPunctuationCleanly(string raw, string expected)
    {
        bool isValid = UrlLauncherValidator.IsValidWebUrl(raw, out var uri);

        isValid.Should().BeTrue();
        uri.Should().NotBeNull();
        uri!.ToString().Should().Be(expected);
    }

    [Fact]
    public void ExtractWebUrls_ExplicitUrlsInPrompt_ExtractsCorrectly()
    {
        string prompt = "Please check https://github.com/harshitthek/HolyHand and also visit http://example.org/docs";

        var urls = UrlLauncherValidator.ExtractWebUrls(prompt);

        urls.Should().HaveCount(2);
        urls[0].AbsoluteUri.Should().Be("https://github.com/harshitthek/HolyHand");
        urls[1].AbsoluteUri.Should().Be("http://example.org/docs");
    }

    [Theory]
    [InlineData("open github.com", "https://github.com/")]
    [InlineData("go to wikipedia.org", "https://wikipedia.org/")]
    [InlineData("visit reddit.com/r/dotnet", "https://reddit.com/r/dotnet")]
    public void ExtractWebUrls_DomainIntent_ConstructsHttpsUrl(string prompt, string expectedUrl)
    {
        var urls = UrlLauncherValidator.ExtractWebUrls(prompt);

        urls.Should().NotBeEmpty();
        urls[0].AbsoluteUri.Should().Be(expectedUrl);
    }

    [Fact]
    public void ExtractWebUrls_SearchOnYoutube_GeneratesYoutubeSearchUrl()
    {
        string prompt = "search for Adele on youtube";

        var urls = UrlLauncherValidator.ExtractWebUrls(prompt);

        urls.Should().ContainSingle();
        urls[0].AbsoluteUri.Should().Be("https://www.youtube.com/results?search_query=Adele");
    }

    [Fact]
    public void ExtractWebUrls_SearchOnBing_GeneratesBingSearchUrl()
    {
        string prompt = "search for c# dotnet on bing";

        var urls = UrlLauncherValidator.ExtractWebUrls(prompt);

        urls.Should().ContainSingle();
        urls[0].AbsoluteUri.Should().Be("https://www.bing.com/search?q=c%23%20dotnet");
    }

    [Fact]
    public void ExtractWebUrls_SearchOnReddit_GeneratesRedditSearchUrl()
    {
        string prompt = "look for rust tutorials in reddit";

        var urls = UrlLauncherValidator.ExtractWebUrls(prompt);

        urls.Should().ContainSingle();
        urls[0].AbsoluteUri.Should().Be("https://www.reddit.com/search/?q=rust%20tutorials");
    }

    [Fact]
    public void ExtractWebUrls_SearchOnWikipedia_GeneratesWikipediaSearchUrl()
    {
        string prompt = "search for Quantum Mechanics on wikipedia";

        var urls = UrlLauncherValidator.ExtractWebUrls(prompt);

        urls.Should().ContainSingle();
        urls[0].AbsoluteUri.Should().Be("https://en.wikipedia.org/wiki/Special:Search?search=Quantum%20Mechanics");
    }

    [Theory]
    [InlineData("google quantum computing", "https://www.google.com/search?q=quantum%20computing")]
    [InlineData("search google for rust language", "https://www.google.com/search?q=rust%20language")]
    [InlineData("please google Alan Turing biography", "https://www.google.com/search?q=Alan%20Turing%20biography")]
    public void ExtractWebUrls_DirectGoogleCommand_GeneratesGoogleUrl(string prompt, string expectedUrl)
    {
        var urls = UrlLauncherValidator.ExtractWebUrls(prompt);

        urls.Should().ContainSingle();
        urls[0].AbsoluteUri.Should().Be(expectedUrl);
    }

    [Theory]
    [InlineData("youtube lofi hip hop radio", "https://www.youtube.com/results?search_query=lofi%20hip%20hop%20radio")]
    [InlineData("search youtube for beethoven 9th symphony", "https://www.youtube.com/results?search_query=beethoven%209th%20symphony")]
    public void ExtractWebUrls_DirectYoutubeCommand_GeneratesYoutubeUrl(string prompt, string expectedUrl)
    {
        var urls = UrlLauncherValidator.ExtractWebUrls(prompt);

        urls.Should().ContainSingle();
        urls[0].AbsoluteUri.Should().Be(expectedUrl);
    }

    [Fact]
    public void ExtractWebUrls_ChainedPlatformSearch_GeneratesCorrectTargetSearch()
    {
        string prompt = "open brave and search for youtube and search honey singh songs";

        var urls = UrlLauncherValidator.ExtractWebUrls(prompt);

        urls.Should().ContainSingle();
        urls[0].AbsoluteUri.Should().Be("https://www.youtube.com/results?search_query=honey%20singh%20songs");
    }

    [Fact]
    public void ExtractWebUrls_ChainedSpotifySearch_GeneratesSpotifySearchUrl()
    {
        string prompt = "open browser then go to spotify and play bohemian rhapsody.";

        var urls = UrlLauncherValidator.ExtractWebUrls(prompt);

        urls.Should().ContainSingle();
        urls[0].AbsoluteUri.Should().Be("https://open.spotify.com/search/bohemian%20rhapsody");
    }

    [Theory]
    [InlineData("play aditya rikhari on spotify", "https://open.spotify.com/search/aditya%20rikhari")]
    [InlineData("stream any song of coldplay on youtube", "https://www.youtube.com/results?search_query=coldplay")]
    [InlineData("listen to songs by Taylor Swift", "https://www.youtube.com/results?search_query=Taylor%20Swift")]
    public void ExtractWebUrls_MusicStreamingIntent_ExtractsMusicSearchUrl(string prompt, string expectedUrl)
    {
        var urls = UrlLauncherValidator.ExtractWebUrls(prompt);

        urls.Should().ContainSingle();
        urls[0].AbsoluteUri.Should().Be(expectedUrl);
    }

    [Theory]
    [InlineData("open brave and search lion", "https://www.google.com/search?q=lion")]
    [InlineData("search about artificial intelligence in bing", "https://www.bing.com/search?q=artificial%20intelligence")]
    [InlineData("query regarding rust compiler on reddit", "https://www.reddit.com/search/?q=rust%20compiler")]
    public void ExtractWebUrls_GeneralSearchIntent_ConstructsCorrectSearchUrl(string prompt, string expectedUrl)
    {
        var urls = UrlLauncherValidator.ExtractWebUrls(prompt);

        urls.Should().ContainSingle();
        urls[0].AbsoluteUri.Should().Be(expectedUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("open notepad and write hello world")]
    [InlineData("click on submit button")]
    [InlineData("close active window")]
    public void ExtractWebUrls_NonWebPrompts_ReturnsEmptyList(string prompt)
    {
        var urls = UrlLauncherValidator.ExtractWebUrls(prompt);

        urls.Should().BeEmpty();
    }
}
