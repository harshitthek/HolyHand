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

    [Theory]
    [InlineData("Submit")]
    [InlineData("Submit Application")]
    [InlineData("Apply Now")]
    [InlineData("Send Email")]
    [InlineData("Pay $50")]
    [InlineData("Buy License")]
    [InlineData("Purchase Ticket")]
    [InlineData("Order Food")]
    [InlineData("Delete Database")]
    [InlineData("Remove User")]
    [InlineData("Post Update")]
    [InlineData("Publish Article")]
    [InlineData("Confirm Transaction")]
    [InlineData("Sign in to Account")]
    [InlineData("Install Package")]
    [InlineData("Run Executable")]
    [InlineData("Uninstall Driver")]
    [InlineData("Transfer Funds")]
    public void RS01_TableDriven_SensitiveVerbs_RequireConfirmation(string label)
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
        reason.Should().Contain(decision.Operation.ToString());
        reason.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("Search")]
    [InlineData("Next")]
    [InlineData("Previous")]
    [InlineData("View Profile")]
    [InlineData("Read More")]
    [InlineData("Refresh Feed")]
    [InlineData("Filter By Name")]
    public void RS01_BenignVerbs_DoNotRequireConfirmation(string label)
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

    [Fact]
    public async Task RS02_ModelRisk_CanEscalate_NeverDowngrade()
    {
        // Case 1: Code policy flags sensitive verb "Delete Account".
        // Even if decision model returned Harmless, code policy insists on confirmation.
        var sensitiveDecision = new AgentDecision
        {
            Operation = AgentOperation.Click,
            TargetId = "e1",
            TargetLabel = "Delete Account"
        };
        var sensitiveElement = new AccessibilityElement { Id = "e1", Role = "Button", Label = "Delete Account" };

        bool codeRequired = _policy.RequiresConfirmation(sensitiveDecision, sensitiveElement, _sampleApp, out _);
        codeRequired.Should().BeTrue(); // Invariant 1: Plain code risk decisions first

        // Case 2: Code policy says benign "Click Filter" is safe, but Jev Call B escalates to Irreversible
        var benignDecision = new AgentDecision
        {
            Operation = AgentOperation.Click,
            TargetId = "e2",
            TargetLabel = "Export and Sync External Service"
        };
        var benignElement = new AccessibilityElement { Id = "e2", Role = "Button", Label = "Export and Sync External Service" };

        bool benignCodeRequired = _policy.RequiresConfirmation(benignDecision, benignElement, _sampleApp, out _);
        benignCodeRequired.Should().BeFalse();

        // Model escalation test inside AgentLoop
        var mockDecisionModel = new Mock<IDecisionModel>();
        mockDecisionModel
            .Setup(m => m.DecideNextActionAsync(It.IsAny<string>(), It.IsAny<AppTarget>(), It.IsAny<IReadOnlyList<AccessibilityElement>>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(benignDecision);
        mockDecisionModel
            .Setup(m => m.EvaluateActionRiskAsync(It.IsAny<string>(), It.IsAny<AppTarget>(), benignDecision, benignElement, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActionRiskScore.IrreversibleOrExternalEffect); // Jev escalates!

        var mockReader = new Mock<IScreenReader>();
        mockReader
            .Setup(r => r.ReadElementsAsync(It.IsAny<AppTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccessibilityElement> { benignElement });

        var mockExecutor = new Mock<IActionExecutor>();
        var mockPrompt = new Mock<IConfirmationPrompt>();
        mockPrompt
            .Setup(p => p.RequestConfirmationAsync(It.IsAny<AgentDecision>(), It.IsAny<AccessibilityElement>(), It.IsAny<AppTarget>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // User rejects escalation

        var loop = new AgentLoop(
            mockReader.Object,
            mockDecisionModel.Object,
            mockExecutor.Object,
            new AgentLoopOptions { MaxSteps = 1 },
            NullLogger<AgentLoop>.Instance,
            _policy,
            mockPrompt.Object);

        var result = await loop.RunAsync("Sync external service", _sampleApp);

        // Verification: confirmation was requested due to model escalation, rejection halted execution
        mockPrompt.Verify(p => p.RequestConfirmationAsync(
            benignDecision,
            benignElement,
            _sampleApp,
            It.Is<string>(s => s.Contains("Model escalated risk")),
            It.IsAny<CancellationToken>()), Times.Once);

        mockExecutor.Verify(e => e.ExecuteAsync(It.IsAny<AgentDecision>(), It.IsAny<AccessibilityElement>(), It.IsAny<CancellationToken>()), Times.Never);
        result.Status.Should().Be(AgentRunStatus.NeedsHumanInput);
    }

    [Fact]
    public async Task RS04_RejectConfirmation_ExecutesNothing_WritesAuditRejected()
    {
        var sensitiveDecision = new AgentDecision
        {
            Operation = AgentOperation.Click,
            TargetId = "e1",
            TargetLabel = "Delete Records"
        };
        var sensitiveEl = new AccessibilityElement { Id = "e1", Role = "Button", Label = "Delete Records" };

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
            .Setup(p => p.RequestConfirmationAsync(It.IsAny<AgentDecision>(), It.IsAny<AccessibilityElement>(), It.IsAny<AppTarget>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Human says REJECT

        var mockAuditLog = new Mock<IAuditLog>();

        var loop = new AgentLoop(
            mockReader.Object,
            mockDecisionModel.Object,
            mockExecutor.Object,
            new AgentLoopOptions { MaxSteps = 1 },
            NullLogger<AgentLoop>.Instance,
            _policy,
            mockPrompt.Object,
            mockAuditLog.Object);

        var result = await loop.RunAsync("Clean up system", _sampleApp);

        // Assert nothing executed
        mockExecutor.Verify(e => e.ExecuteAsync(It.IsAny<AgentDecision>(), It.IsAny<AccessibilityElement>(), It.IsAny<CancellationToken>()), Times.Never);
        result.Status.Should().Be(AgentRunStatus.NeedsHumanInput);

        // Assert audit entry logged as "rejected"
        mockAuditLog.Verify(a => a.LogAsync(
            It.Is<AuditLogEntry>(e => e.DecisionType == "rejected" && e.Operation == AgentOperation.Click),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RS05_ApproveConfirmation_ExecutesAction_WritesAuditConfirmed()
    {
        var sensitiveDecision = new AgentDecision
        {
            Operation = AgentOperation.Click,
            TargetId = "e1",
            TargetLabel = "Confirm Payment"
        };
        var sensitiveEl = new AccessibilityElement { Id = "e1", Role = "Button", Label = "Confirm Payment" };

        var mockReader = new Mock<IScreenReader>();
        mockReader
            .Setup(r => r.ReadElementsAsync(It.IsAny<AppTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccessibilityElement> { sensitiveEl });

        var mockDecisionModel = new Mock<IDecisionModel>();
        mockDecisionModel
            .Setup(m => m.DecideNextActionAsync(It.IsAny<string>(), It.IsAny<AppTarget>(), It.IsAny<IReadOnlyList<AccessibilityElement>>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sensitiveDecision);

        var mockExecutor = new Mock<IActionExecutor>();
        mockExecutor
            .Setup(e => e.ExecuteAsync(sensitiveDecision, sensitiveEl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActionResult.SuccessResult("Clicked button"));

        var mockPrompt = new Mock<IConfirmationPrompt>();
        mockPrompt
            .Setup(p => p.RequestConfirmationAsync(It.IsAny<AgentDecision>(), It.IsAny<AccessibilityElement>(), It.IsAny<AppTarget>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Human says APPROVE

        var mockAuditLog = new Mock<IAuditLog>();

        var loop = new AgentLoop(
            mockReader.Object,
            mockDecisionModel.Object,
            mockExecutor.Object,
            new AgentLoopOptions { MaxSteps = 1 },
            NullLogger<AgentLoop>.Instance,
            _policy,
            mockPrompt.Object,
            mockAuditLog.Object);

        await loop.RunAsync("Pay invoice", _sampleApp);

        // Assert executed exactly once
        mockExecutor.Verify(e => e.ExecuteAsync(sensitiveDecision, sensitiveEl, It.IsAny<CancellationToken>()), Times.Once);

        // Assert audit entry logged as "confirmed"
        mockAuditLog.Verify(a => a.LogAsync(
            It.Is<AuditLogEntry>(e => e.DecisionType == "confirmed" && e.Operation == AgentOperation.Click),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("1password", "1Password Manager")]
    [InlineData("bitwarden", "Bitwarden Vault")]
    [InlineData("keepass", "KeePass Password Safe")]
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

        // Verification: App refused immediately with clear error
        result.Status.Should().Be(AgentRunStatus.Failed);
        result.Message.Should().Contain("deny-list");

        // Zero screen reads and zero executions
        mockReader.Verify(r => r.ReadElementsAsync(It.IsAny<AppTarget>(), It.IsAny<CancellationToken>()), Times.Never);
        mockExecutor.Verify(e => e.ExecuteAsync(It.IsAny<AgentDecision>(), It.IsAny<AccessibilityElement>(), It.IsAny<CancellationToken>()), Times.Never);

        // Audit entry recorded with "denied"
        mockAuditLog.Verify(a => a.LogAsync(
            It.Is<AuditLogEntry>(e => e.DecisionType == "denied" && e.AppProcess == processName),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
