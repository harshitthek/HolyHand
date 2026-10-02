using System.Drawing;
using FluentAssertions;
using HolyHand.Core.Models;
using HolyHand.Core.Safety;
using Xunit;

namespace HolyHand.Tests.Safety;

public class RiskPolicyExtendedTests
{
    private readonly RiskPolicy _defaultPolicy = new();

    private static AppTarget CreateTarget(string processName, string windowTitle) => new()
    {
        ProcessId = 9999,
        ProcessName = processName,
        WindowTitle = windowTitle,
        WindowBounds = new Rectangle(0, 0, 1024, 768)
    };

    [Theory]
    [InlineData("1Password", "1Password Desktop")]
    [InlineData("Bitwarden", "Bitwarden Password Manager")]
    [InlineData("KeePassXC", "KeePassXC - Vault")]
    [InlineData("enpass", "Enpass Vault")]
    [InlineData("dashlane", "Dashlane")]
    [InlineData("LastPass", "LastPass Vault")]
    public void IsAppDenied_DenyListedPasswordManagers_ReturnsTrue(string proc, string title)
    {
        var target = CreateTarget(proc, title);

        bool denied = _defaultPolicy.IsAppDenied(target, out var reason);

        denied.Should().BeTrue();
        reason.Should().Contain("deny-list");
    }

    [Theory]
    [InlineData("notepad", "Untitled - Notepad")]
    [InlineData("calc", "Calculator")]
    [InlineData("chrome", "Google Chrome")]
    [InlineData("spotify", "Spotify Free")]
    [InlineData("explorer", "File Explorer")]
    public void IsAppDenied_RegularApplications_ReturnsFalse(string proc, string title)
    {
        var target = CreateTarget(proc, title);

        bool denied = _defaultPolicy.IsAppDenied(target, out var reason);

        denied.Should().BeFalse();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData(AgentOperation.Done)]
    [InlineData(AgentOperation.AskUser)]
    [InlineData(AgentOperation.Wait)]
    public void RequiresConfirmation_BenignControlFlow_ReturnsFalse(AgentOperation op)
    {
        var decision = new AgentDecision { Operation = op };
        var app = CreateTarget("notepad", "Notepad");

        bool requires = _defaultPolicy.RequiresConfirmation(decision, null, app, out var reason);

        requires.Should().BeFalse();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData(AgentOperation.ScrollDown)]
    [InlineData(AgentOperation.ScrollUp)]
    [InlineData(AgentOperation.PressTab)]
    [InlineData(AgentOperation.PressEscape)]
    public void RequiresConfirmation_PureNavigationOperations_ReturnsFalse(AgentOperation op)
    {
        var decision = new AgentDecision { Operation = op };
        var app = CreateTarget("notepad", "Notepad");

        bool requires = _defaultPolicy.RequiresConfirmation(decision, null, app, out var reason);

        requires.Should().BeFalse();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void RequiresConfirmation_ModelRiskEscalation_RequiresConfirmation()
    {
        var decision = new AgentDecision
        {
            Operation = AgentOperation.Click,
            RiskScore = ActionRiskScore.IrreversibleOrExternalEffect
        };
        var app = CreateTarget("notepad", "Notepad");

        bool requires = _defaultPolicy.RequiresConfirmation(decision, null, app, out var reason);

        requires.Should().BeTrue();
        reason.Should().Contain("risk score");
    }

    [Theory]
    [InlineData("PasswordBox", "Enter Password")]
    [InlineData("Edit", "Your SSN")]
    [InlineData("Edit", "Social Security Number")]
    [InlineData("Edit", "Enter PIN")]
    public void RequiresConfirmation_SensitiveOrSecretFields_RequiresConfirmation(string role, string label)
    {
        var decision = new AgentDecision { Operation = AgentOperation.Click, TargetId = "f1" };
        var target = new AccessibilityElement { Id = "f1", Role = role, Label = label };
        var app = CreateTarget("notepad", "Notepad");

        bool requires = _defaultPolicy.RequiresConfirmation(decision, target, app, out var reason);

        requires.Should().BeTrue();
        reason.Should().Contain("sensitive/password field");
    }

    [Fact]
    public void RequiresConfirmation_PinnedTrackInSpotify_DoesNotFalsePositiveOnPin()
    {
        // Word boundary check: "pinned" should NOT match "pin"
        var decision = new AgentDecision { Operation = AgentOperation.Click, TargetId = "btn1", TargetLabel = "Play pinned track" };
        var target = new AccessibilityElement { Id = "btn1", Role = "Button", Label = "Play pinned track" };
        var app = CreateTarget("spotify", "Spotify Free");

        bool requires = _defaultPolicy.RequiresConfirmation(decision, target, app, out var reason);

        requires.Should().BeFalse();
    }

    [Theory]
    [InlineData("Submit Application")]
    [InlineData("Pay Invoice")]
    [InlineData("Buy Subscription")]
    [InlineData("Purchase Ticket")]
    [InlineData("Transfer Funds")]
    [InlineData("Install Update")]
    [InlineData("Publish Post")]
    public void RequiresConfirmation_SensitiveVerbsInTargetLabel_RequiresConfirmation(string label)
    {
        var decision = new AgentDecision { Operation = AgentOperation.Click, TargetId = "b1", TargetLabel = label };
        var target = new AccessibilityElement { Id = "b1", Role = "Button", Label = label };
        var app = CreateTarget("chrome", "Browser");

        bool requires = _defaultPolicy.RequiresConfirmation(decision, target, app, out var reason);

        requires.Should().BeTrue();
        reason.Should().Contain("sensitive verb");
    }

    [Fact]
    public void RequiresConfirmation_SensitiveVerbInTypedText_RequiresConfirmation()
    {
        var decision = new AgentDecision
        {
            Operation = AgentOperation.TypeText,
            TargetId = "txt1",
            TextValue = "Please transfer $500 to savings"
        };
        var app = CreateTarget("chrome", "Banking");

        bool requires = _defaultPolicy.RequiresConfirmation(decision, null, app, out var reason);

        requires.Should().BeTrue();
        reason.Should().Contain("sensitive verb");
    }

    [Theory]
    [InlineData("delete the entire database")]
    [InlineData("erase all documents")]
    [InlineData("destroy the customer records")]
    [InlineData("wipe user history")]
    [InlineData("format the hard drive")]
    public void IsGoalProhibited_DeletionGoals_ReturnsTrue(string goal)
    {
        bool prohibited = _defaultPolicy.IsGoalProhibited(goal, out var reason);

        prohibited.Should().BeTrue();
        reason.Should().Contain("prohibited");
    }

    [Theory]
    [InlineData("create a new document")]
    [InlineData("search for songs by Adele")]
    [InlineData("open notepad and write a summary")]
    [InlineData("read the latest email")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsGoalProhibited_SafeGoals_ReturnsFalse(string goal)
    {
        bool prohibited = _defaultPolicy.IsGoalProhibited(goal, out var reason);

        prohibited.Should().BeFalse();
        reason.Should().BeEmpty();
    }
}
