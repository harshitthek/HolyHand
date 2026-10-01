using System.Drawing;
using FluentAssertions;
using HolyHand.Core.Agent;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Models;
using HolyHand.Core.Safety;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HolyHand.Tests.Safety;

/// <summary>
/// Jarvis-mode risk policy tests.
/// Policy: execute everything automatically EXCEPT deletion operations which are strictly prohibited.
/// No confirmation dialogs for any safe action.
/// </summary>
public class RiskPolicyTests
{
    private readonly RiskPolicy _policy = new();
    private readonly AppTarget _sampleApp = new()
    {
        ProcessId = 1234,
        ProcessName = "chrome",
        WindowTitle = "Mock Application Form",
        WindowHandle = (IntPtr)0x1234,
        WindowBounds = new Rectangle(0, 0, 800, 600)
    };

    // RS01: Sensitive verbs unconditionally require human confirmation
    [Theory]
    [InlineData("Submit")]
    [InlineData("Submit Application")]
    [InlineData("Apply Now")]
    [InlineData("Send Email")]
    [InlineData("Pay $50")]
    [InlineData("Buy License")]
    [InlineData("Purchase Ticket")]
    [InlineData("Order Food")]
    [InlineData("Post Update")]
    [InlineData("Publish Article")]
    [InlineData("Confirm Transaction")]
    [InlineData("Install Package")]
    [InlineData("Transfer Funds")]
    public void RS01_SensitiveVerbs_RequireConfirmation(string label)
    {
        var decision = new AgentDecision
        {
            Operation = AgentOperation.Click,
            TargetId = "e1",
            TargetLabel = label
        };

        var element = new AccessibilityElement
        {
            Id = "e1",
            Role = "Button",
            Label = label,
            Enabled = true
        };

        bool required = _policy.RequiresConfirmation(decision, element, _sampleApp, out var reason);

        required.Should().BeTrue();
        reason.Should().NotBeEmpty();
    }

    // RS01: Benign routine actions never require confirmation
    [Theory]
    [InlineData("Spotify pinned")]
    [InlineData("Search")]
    [InlineData("Next")]
    [InlineData("Previous")]
    [InlineData("View Profile")]
    [InlineData("Read More")]
    [InlineData("Refresh Feed")]
    [InlineData("Filter By Name")]
    public void RS01_BenignActions_NeverRequireConfirmation(string label)
    {
        var decision = new AgentDecision
        {
            Operation = AgentOperation.Click,
            TargetId = "e1",
            TargetLabel = label
        };

        var element = new AccessibilityElement
        {
            Id = "e1",
            Role = "Button",
            Label = label,
            Enabled = true
        };

        bool required = _policy.RequiresConfirmation(decision, element, _sampleApp, out var reason);

        required.Should().BeFalse();
        reason.Should().BeEmpty();
    }

    // RS02: Model risk escalation — when model escalates risk, confirmation is required
    [Fact]
    public void RS02_ModelRisk_CanEscalate_RequiresConfirmation()
    {
        var escalatedDecision = new AgentDecision
        {
            Operation = AgentOperation.Click,
            TargetId = "e2",
            TargetLabel = "Export and Sync External Service",
            RiskScore = ActionRiskScore.IrreversibleOrExternalEffect
        };
        var benignElement = new AccessibilityElement { Id = "e2", Role = "Button", Label = "Export and Sync External Service" };

        bool required = _policy.RequiresConfirmation(escalatedDecision, benignElement, _sampleApp, out var reason);
        required.Should().BeTrue();
        reason.Should().Contain("meets or exceeds confirmation threshold");
    }

    // RS04: Sensitive action requires confirmation; when approved, it executes
    [Fact]
    public async Task RS04_SensitiveAction_RequiresConfirmation_WhenApproved_Executes()
    {
        var sensitiveDecision = new AgentDecision
        {
            Operation = AgentOperation.Click,
            TargetId = "e1",
            TargetLabel = "Submit Application"
        };
        var sensitiveEl = new AccessibilityElement { Id = "e1", Role = "Button", Label = "Submit Application" };

        var mockReader = new Mock<IScreenReader>();
        mockReader
            .Setup(r => r.ReadElementsAsync(It.IsAny<AppTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccessibilityElement> { sensitiveEl });

        var mockDecisionModel = new Mock<IDecisionModel>();
        var callCount = 0;
        mockDecisionModel
            .Setup(m => m.DecideNextActionAsync(It.IsAny<string>(), It.IsAny<AppTarget>(), It.IsAny<IReadOnlyList<AccessibilityElement>>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? sensitiveDecision
                    : new AgentDecision { Operation = AgentOperation.Done };
            });
        mockDecisionModel
            .Setup(m => m.VerifyCompletionAsync(It.IsAny<string>(), It.IsAny<AppTarget>(), It.IsAny<IReadOnlyList<AccessibilityElement>>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockExecutor = new Mock<IActionExecutor>();
        mockExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<AgentDecision>(), It.IsAny<AccessibilityElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActionResult.SuccessResult("Executed"));

        var mockPrompt = new Mock<IConfirmationPrompt>();
        mockPrompt
            .Setup(p => p.RequestConfirmationAsync(
                It.IsAny<AgentDecision>(),
                It.IsAny<AccessibilityElement>(),
                It.IsAny<AppTarget>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // User approves

        var mockAuditLog = new Mock<IAuditLog>();

        var loop = new AgentLoop(
            mockReader.Object,
            mockDecisionModel.Object,
            mockExecutor.Object,
            new AgentLoopOptions { MaxSteps = 5 },
            NullLogger<AgentLoop>.Instance,
            _policy,
            mockPrompt.Object,
            mockAuditLog.Object);

        var result = await loop.RunAsync("Fill and submit form", _sampleApp);

        mockPrompt.Verify(p => p.RequestConfirmationAsync(
            It.Is<AgentDecision>(d => d.TargetId == "e1"),
            It.Is<AccessibilityElement>(e => e.Id == "e1"),
            It.IsAny<AppTarget>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);

        mockExecutor.Verify(e => e.ExecuteAsync(sensitiveDecision, sensitiveEl, It.IsAny<CancellationToken>()), Times.Once);
        mockAuditLog.Verify(a => a.LogAsync(
            It.Is<AuditLogEntry>(e => e.DecisionType == "confirmed"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // RS04: Sensitive action requires confirmation; when rejected, execution halts
    [Fact]
    public async Task RS04_SensitiveAction_RequiresConfirmation_WhenRejected_HaltsExecution()
    {
        var sensitiveDecision = new AgentDecision
        {
            Operation = AgentOperation.Click,
            TargetId = "e1",
            TargetLabel = "Submit Application"
        };
        var sensitiveEl = new AccessibilityElement { Id = "e1", Role = "Button", Label = "Submit Application" };

        var mockReader = new Mock<IScreenReader>();
        mockReader
            .Setup(r => r.ReadElementsAsync(It.IsAny<AppTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccessibilityElement> { sensitiveEl });

        var mockDecisionModel = new Mock<IDecisionModel>();
        mockDecisionModel
            .Setup(m => m.DecideNextActionAsync(It.IsAny<string>(), It.IsAny<AppTarget>(), It.IsAny<IReadOnlyList<AccessibilityElement>>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sensitiveDecision);

        var mockExecutor = new Mock<IActionExecutor>();
        var mockPrompt = new Mock<IConfirmationPrompt>();
        mockPrompt
            .Setup(p => p.RequestConfirmationAsync(
                It.IsAny<AgentDecision>(),
                It.IsAny<AccessibilityElement>(),
                It.IsAny<AppTarget>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // User rejects

        var mockAuditLog = new Mock<IAuditLog>();

        var loop = new AgentLoop(
            mockReader.Object,
            mockDecisionModel.Object,
            mockExecutor.Object,
            new AgentLoopOptions { MaxSteps = 5 },
            NullLogger<AgentLoop>.Instance,
            _policy,
            mockPrompt.Object,
            mockAuditLog.Object);

        var result = await loop.RunAsync("Fill and submit form", _sampleApp);

        result.Status.Should().Be(AgentRunStatus.NeedsHumanInput);
        result.Message.Should().Contain("rejected by human");

        mockExecutor.Verify(e => e.ExecuteAsync(It.IsAny<AgentDecision>(), It.IsAny<AccessibilityElement>(), It.IsAny<CancellationToken>()), Times.Never);
        mockAuditLog.Verify(a => a.LogAsync(
            It.Is<AuditLogEntry>(e => e.DecisionType == "rejected"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // RS05: Password field clicks require confirmation
    [Fact]
    public void RS05_PasswordField_RequiresConfirmation()
    {
        var passwordDecision = new AgentDecision
        {
            Operation = AgentOperation.Click,
            TargetId = "pw1",
            TargetLabel = "Password"
        };
        var passwordElement = new AccessibilityElement
        {
            Id = "pw1",
            Role = "PasswordBox",
            Label = "Password",
            Value = "[PASSWORD]",
            Enabled = true
        };

        bool required = _policy.RequiresConfirmation(passwordDecision, passwordElement, _sampleApp, out var reason);

        required.Should().BeTrue();
        reason.Should().Contain("sensitive/password field");
    }

    // RS06: Sensitive typed text requires confirmation
    [Fact]
    public void RS06_SensitiveTypedText_RequiresConfirmation()
    {
        var typeDecision = new AgentDecision
        {
            Operation = AgentOperation.TypeText,
            TargetId = "e1",
            TargetLabel = "Input Box",
            TextValue = "submit login transfer"
        };

        bool required = _policy.RequiresConfirmation(typeDecision, null, _sampleApp, out var reason);

        required.Should().BeTrue();
        reason.Should().Contain("Typed text contains sensitive verb");
    }

    [Theory]
    [InlineData("chrome", "Submit Application Form")]
    [InlineData("brave", "Google Search")]
    [InlineData("spotify", "Spotify")]
    [InlineData("explorer", "Program Manager")]
    public void RS08_AllApps_AreAllowed_ExceptPasswordManagers(string processName, string windowTitle)
    {
        var app = new AppTarget
        {
            ProcessId = 1111,
            ProcessName = processName,
            WindowTitle = windowTitle,
            WindowHandle = (IntPtr)0x1111,
            WindowBounds = new Rectangle(0, 0, 800, 600)
        };

        bool denied = _policy.IsAppDenied(app, out var reason);

        denied.Should().BeFalse();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("1password", "1Password")]
    [InlineData("bitwarden", "Bitwarden")]
    [InlineData("keepass", "KeePass")]
    [InlineData("keepassxc", "KeePassXC Password Safe")]
    [InlineData("lastpass", "LastPass Vault")]
    [InlineData("dashlane", "Dashlane")]
    public async Task RS09_DenyListedApps_AreRefusedImmediately(string processName, string windowTitle)
    {
        var deniedApp = new AppTarget
        {
            ProcessId = 9999,
            ProcessName = processName,
            WindowTitle = windowTitle,
            WindowHandle = (IntPtr)0x9999,
            WindowBounds = new Rectangle(0, 0, 800, 600)
        };

        var mockReader = new Mock<IScreenReader>();
        var mockDecisionModel = new Mock<IDecisionModel>();
        var mockExecutor = new Mock<IActionExecutor>();
        var mockAuditLog = new Mock<IAuditLog>();

        var loop = new AgentLoop(
            mockReader.Object,
            mockDecisionModel.Object,
            mockExecutor.Object,
            new AgentLoopOptions { MaxSteps = 5 },
            NullLogger<AgentLoop>.Instance,
            _policy,
            null,
            mockAuditLog.Object);

        var result = await loop.RunAsync("Copy password", deniedApp);

        result.Status.Should().Be(AgentRunStatus.Failed);
        result.Message.Should().Contain("deny-list");

        mockReader.Verify(r => r.ReadElementsAsync(It.IsAny<AppTarget>(), It.IsAny<CancellationToken>()), Times.Never);
        mockExecutor.Verify(e => e.ExecuteAsync(It.IsAny<AgentDecision>(), It.IsAny<AccessibilityElement>(), It.IsAny<CancellationToken>()), Times.Never);

        mockAuditLog.Verify(a => a.LogAsync(
            It.Is<AuditLogEntry>(e => e.DecisionType == "denied" && e.AppProcess == processName),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("delete all temp files")]
    [InlineData("erase all user data")]
    [InlineData("wipe hard disk")]
    [InlineData("destroy current session")]
    [InlineData("del secret.txt")]
    [InlineData("format c:")]
    public void RS10_DeletionGoals_AreStrictlyProhibited(string goal)
    {
        bool prohibited = _policy.IsGoalProhibited(goal, out var reason);

        prohibited.Should().BeTrue();
        reason.Should().ContainEquivalentOf("prohibited");
    }

    [Theory]
    [InlineData("open spotify")]
    [InlineData("search for Adele on youtube")]
    [InlineData("write hello world in notepad")]
    [InlineData("launch calculator")]
    [InlineData("open google chrome")]
    [InlineData("submit the application form")]
    [InlineData("send an email to john")]
    [InlineData("install the app")]
    [InlineData("transfer funds to savings")]
    public void RS10_BenignGoals_AreNotProhibited(string goal)
    {
        bool prohibited = _policy.IsGoalProhibited(goal, out var reason);

        prohibited.Should().BeFalse();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Delete")]
    [InlineData("Erase All")]
    [InlineData("Wipe Disk")]
    public void RS11_DeletionActionLabels_AreStrictlyProhibited(string label)
    {
        var decision = new AgentDecision
        {
            Operation = AgentOperation.Click,
            TargetId = "e_del",
            TargetLabel = label
        };

        var element = new AccessibilityElement
        {
            Id = "e_del",
            Role = "Button",
            Label = label,
            Enabled = true
        };

        bool prohibited = _policy.IsActionProhibited(decision, element, "clean up", out var reason);

        prohibited.Should().BeTrue();
        reason.Should().ContainEquivalentOf("prohibited");
    }

    [Fact]
    public async Task RS12_AgentLoop_AbortsImmediately_OnProhibitedGoal()
    {
        var mockReader = new Mock<IScreenReader>();
        var mockDecisionModel = new Mock<IDecisionModel>();
        var mockExecutor = new Mock<IActionExecutor>();
        var mockPrompt = new Mock<IConfirmationPrompt>();
        var mockAuditLog = new Mock<IAuditLog>();

        var loop = new AgentLoop(
            mockReader.Object,
            mockDecisionModel.Object,
            mockExecutor.Object,
            new AgentLoopOptions { MaxSteps = 5 },
            NullLogger<AgentLoop>.Instance,
            _policy,
            mockPrompt.Object,
            mockAuditLog.Object);

        var result = await loop.RunAsync("delete my files in notepad", _sampleApp);

        result.Status.Should().Be(AgentRunStatus.Failed);
        result.StepsCompleted.Should().Be(0);
        result.Message.Should().Contain("Prohibited");

        mockReader.Verify(r => r.ReadElementsAsync(It.IsAny<AppTarget>(), It.IsAny<CancellationToken>()), Times.Never);
        mockExecutor.Verify(e => e.ExecuteAsync(It.IsAny<AgentDecision>(), It.IsAny<AccessibilityElement>(), It.IsAny<CancellationToken>()), Times.Never);
        mockPrompt.Verify(p => p.RequestConfirmationAsync(It.IsAny<AgentDecision>(), It.IsAny<AccessibilityElement>(), It.IsAny<AppTarget>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        mockAuditLog.Verify(a => a.LogAsync(
            It.Is<AuditLogEntry>(e => e.DecisionType == "prohibited" && e.Goal.Contains("delete")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RS13_AgentLoop_AbortsImmediately_OnProhibitedAction()
    {
        var prohibitedDecision = new AgentDecision
        {
            Operation = AgentOperation.Click,
            TargetId = "del_btn",
            TargetLabel = "Delete"
        };
        var prohibitedEl = new AccessibilityElement { Id = "del_btn", Role = "Button", Label = "Delete" };

        var mockReader = new Mock<IScreenReader>();
        mockReader
            .Setup(r => r.ReadElementsAsync(It.IsAny<AppTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccessibilityElement> { prohibitedEl });

        var mockDecisionModel = new Mock<IDecisionModel>();
        mockDecisionModel
            .Setup(m => m.DecideNextActionAsync(It.IsAny<string>(), It.IsAny<AppTarget>(), It.IsAny<IReadOnlyList<AccessibilityElement>>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prohibitedDecision);

        var mockExecutor = new Mock<IActionExecutor>();
        var mockPrompt = new Mock<IConfirmationPrompt>();
        var mockAuditLog = new Mock<IAuditLog>();

        var loop = new AgentLoop(
            mockReader.Object,
            mockDecisionModel.Object,
            mockExecutor.Object,
            new AgentLoopOptions { MaxSteps = 5 },
            NullLogger<AgentLoop>.Instance,
            _policy,
            mockPrompt.Object,
            mockAuditLog.Object);

        var result = await loop.RunAsync("organize documents", _sampleApp);

        result.Status.Should().Be(AgentRunStatus.Failed);
        result.Message.Should().Contain("Prohibited");

        mockExecutor.Verify(e => e.ExecuteAsync(It.IsAny<AgentDecision>(), It.IsAny<AccessibilityElement>(), It.IsAny<CancellationToken>()), Times.Never);
        mockPrompt.Verify(p => p.RequestConfirmationAsync(It.IsAny<AgentDecision>(), It.IsAny<AccessibilityElement>(), It.IsAny<AppTarget>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        mockAuditLog.Verify(a => a.LogAsync(
            It.Is<AuditLogEntry>(e => e.DecisionType == "prohibited" && e.Operation == AgentOperation.Click),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}


