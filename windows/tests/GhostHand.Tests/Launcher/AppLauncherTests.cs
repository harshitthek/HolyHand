using FluentAssertions;
using HolyHand.Core.Agent;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Jev;
using HolyHand.Core.Models;
using HolyHand.Platform.Launcher;
using Moq;
using Xunit;

namespace HolyHand.Tests.Launcher;

public class AppLauncherTests
{
    private readonly AppLauncher _launcher = new();

    [Theory]
    [InlineData("open settings", "settings", "ms-settings:")]
    [InlineData("please open windows settings", "windows settings", "ms-settings:")]
    [InlineData("launch notepad", "notepad", "notepad.exe")]
    [InlineData("start calculator and calculate 5 + 5", "calculator", "calc.exe")]
    [InlineData("open chrome and search for Adele", "chrome", "chrome.exe")]
    [InlineData("open file explorer", "file explorer", "explorer.exe")]
    [InlineData("open task manager", "task manager", "taskmgr.exe")]
    public void AL01_TryExtractAppLaunch_RecognizesValidAppsAndCommands(string goal, string expectedApp, string expectedCommand)
    {
        bool success = _launcher.TryExtractAppLaunch(goal, out var appName, out var launchCommand);

        success.Should().BeTrue();
        appName.ToLowerInvariant().Should().Be(expectedApp.ToLowerInvariant());
        launchCommand.ToLowerInvariant().Should().Be(expectedCommand.ToLowerInvariant());
    }

    [Theory]
    [InlineData("powershell.exe -enc dGVzdA==")]
    [InlineData("cmd.exe /c del *.*")]
    [InlineData("rundll32.exe")]
    [InlineData("format.com")]
    public async Task AL02_MaliciousCommands_AreBlockedBySafetyPolicy(string dangerousCommand)
    {
        var act = async () => await _launcher.LaunchAppAsync("malicious", dangerousCommand);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*safety policy*");
    }

    [Fact]
    public void AL03_TryExtractUrlLaunch_ExtractsHttpAndHttpsUrls()
    {
        bool success = _launcher.TryExtractUrlLaunch("open HolyHand to check release", out var url);

        success.Should().BeTrue();
        url.ToString().Should().Be("HolyHand");
    }

    [Fact]
    public void AL04_CandidateChoices_IncludeOpenAppAndOpenUrl_WhenPresentInGoal()
    {
        var candidates = JevDecisionModel.ExtractAppLaunchCandidates("open settings");
        candidates.Should().Contain(c => c.Equals("settings", StringComparison.OrdinalIgnoreCase));

        var urlCandidates = UrlLauncherValidator.ExtractWebUrls("open https://news.ycombinator.com");
        urlCandidates.Should().NotBeEmpty();
        urlCandidates[0].Host.Should().Be("news.ycombinator.com");
    }

    [Fact]
    public async Task AL05_AgentLoop_TransitionsTargetWindow_WhenAppIsLaunched()
    {
        // Arrange
        var mockScreenReader = new Mock<IScreenReader>();
        var mockDecisionModel = new Mock<IDecisionModel>();
        var mockActionExecutor = new Mock<IActionExecutor>();

        var initialTarget = new AppTarget
        {
            ProcessId = 1000,
            ProcessName = "explorer",
            WindowTitle = "Program Manager",
            WindowHandle = new IntPtr(0x1000)
        };

        var launchedTarget = new AppTarget
        {
            ProcessId = 2000,
            ProcessName = "SystemSettings",
            WindowTitle = "Settings",
            WindowHandle = new IntPtr(0x2000)
        };

        mockScreenReader.Setup(r => r.ReadElementsAsync(It.IsAny<AppTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccessibilityElement>());

        // Step 1: Decision to open settings
        mockDecisionModel.Setup(d => d.DecideNextActionAsync(It.IsAny<string>(), It.IsAny<AppTarget>(), It.IsAny<IReadOnlyList<AccessibilityElement>>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentDecision
            {
                Operation = AgentOperation.OpenApp,
                TargetId = "settings",
                Confidence = 0.99
            });

        // Executor returns NewTarget on OpenApp
        mockActionExecutor.Setup(e => e.ExecuteAsync(It.IsAny<AgentDecision>(), It.IsAny<AccessibilityElement?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActionResult.TargetChanged(launchedTarget, "Launched SystemSettings"));

        var loop = new AgentLoop(
            mockScreenReader.Object,
            mockDecisionModel.Object,
            mockActionExecutor.Object,
            new AgentLoopOptions { MaxSteps = 5, DryRun = false },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentLoop>.Instance);

        // Act
        var result = await loop.RunAsync("open settings", initialTarget);

        // Assert
        result.Status.Should().Be(AgentRunStatus.Completed);
        mockActionExecutor.Verify(e => e.ExecuteAsync(It.Is<AgentDecision>(d => d.Operation == AgentOperation.OpenApp), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("search for Adele on youtube", "youtube.com", "Adele")]
    [InlineData("search for quantum computing on google", "google.com", "quantum")]
    [InlineData("google current weather", "google.com", "weather")]
    [InlineData("open youtube", "youtube.com", "")]
    [InlineData("open github", "github.com", "")]
    public void AL06_TryExtractUrlLaunch_SynthesizesWebSearchesAndSites(string goal, string expectedHost, string expectedQueryFragment)
    {
        bool success = _launcher.TryExtractUrlLaunch(goal, out var url);

        success.Should().BeTrue();
        url.Host.Should().Contain(expectedHost);
        if (!string.IsNullOrEmpty(expectedQueryFragment))
        {
            url.Query.Should().Contain(expectedQueryFragment);
        }
    }

    [Theory]
    [InlineData("switch to discord", "discord")]
    [InlineData("start spotify", "spotify")]
    [InlineData("open vlc", "vlc")]
    public void AL07_TryExtractAppLaunch_SupportsUniversalApps(string goal, string expectedApp)
    {
        bool success = _launcher.TryExtractAppLaunch(goal, out var appName, out _);

        success.Should().BeTrue();
        appName.ToLowerInvariant().Should().Be(expectedApp.ToLowerInvariant());
    }
}
