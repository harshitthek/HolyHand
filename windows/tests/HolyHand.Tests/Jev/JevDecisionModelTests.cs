using System.Drawing;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using HolyHand.Core.Jev;
using HolyHand.Core.Models;
using HolyHand.Core.Safety;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HolyHand.Tests.Jev;

public class JevDecisionModelTests
{
    private readonly Mock<IJevClient> _mockClient = new();
    private readonly JevOptions _options = new()
    {
        ApiKey = "test-key",
        ModelId = "jev-latest",
        DecisionConfidenceThreshold = 0.50
    };

    private JevDecisionModel CreateModel() =>
        new(_mockClient.Object, _options, NullLogger<JevDecisionModel>.Instance);

    private static AppTarget CreateTarget() => new()
    {
        ProcessId = 4321,
        ProcessName = "notepad",
        WindowTitle = "Untitled - Notepad",
        WindowBounds = new Rectangle(0, 0, 800, 600)
    };

    private static JsonElement CreateBooleanAnswer(double prob) =>
        JsonDocument.Parse($"{{\"type\":\"boolean\",\"probability\":{prob.ToString(CultureInfo.InvariantCulture)}}}").RootElement.Clone();

    private static JsonElement CreateChoiceAnswer(string choice, double confidence, Dictionary<string, double>? probs = null)
    {
        var probsJson = probs != null
            ? string.Join(",", probs.Select(kv => $"\"{kv.Key}\":{kv.Value.ToString(CultureInfo.InvariantCulture)}"))
            : $"\"{choice}\":{confidence.ToString(CultureInfo.InvariantCulture)}";
        return JsonDocument.Parse($"{{\"type\":\"choice\",\"choice\":\"{choice}\",\"confidence\":{confidence.ToString(CultureInfo.InvariantCulture)},\"probabilities\":{{{probsJson}}}}}").RootElement.Clone();
    }

    private static JsonElement CreateScoreAnswer(int score, List<double> probs)
    {
        var probsJson = string.Join(",", probs.Select(p => p.ToString(CultureInfo.InvariantCulture)));
        return JsonDocument.Parse($"{{\"type\":\"score\",\"score\":{score},\"probabilities\":[{probsJson}]}}").RootElement.Clone();
    }

    [Fact]
    public async Task DecideNextActionAsync_GoalAlreadyAchieved_ReturnsDoneOperation()
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["goalAchieved"] = CreateBooleanAnswer(0.92)
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var model = CreateModel();
        var elements = new List<AccessibilityElement>();
        var decision = await model.DecideNextActionAsync("open notepad", CreateTarget(), elements, Array.Empty<string>());

        decision.Operation.Should().Be(AgentOperation.Done);
        decision.Reason.Should().Contain("Goal achieved");
        decision.Confidence.Should().Be(0.92);
    }

    [Fact]
    public async Task DecideNextActionAsync_GoalAchievedBelowThreshold_ContinuesToNextAction()
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["goalAchieved"] = CreateBooleanAnswer(0.40), // below 0.50 threshold
                ["nextAction"] = CreateChoiceAnswer("press:enter", 0.85)
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var model = CreateModel();
        var elements = new List<AccessibilityElement>();
        var decision = await model.DecideNextActionAsync("press enter", CreateTarget(), elements, Array.Empty<string>());

        decision.Operation.Should().Be(AgentOperation.PressReturn);
    }

    [Fact]
    public async Task DecideNextActionAsync_NextActionConfidenceBelowThreshold_ReturnsAskUser()
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["goalAchieved"] = CreateBooleanAnswer(0.10),
                ["nextAction"] = CreateChoiceAnswer("click:e1", 0.35) // below 0.50
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var model = CreateModel();
        var elements = new List<AccessibilityElement> { new() { Id = "e1", Role = "Button", Label = "Save" } };
        var decision = await model.DecideNextActionAsync("save document", CreateTarget(), elements, Array.Empty<string>());

        decision.Operation.Should().Be(AgentOperation.AskUser);
        decision.Reason.Should().Contain("below threshold");
        decision.Confidence.Should().Be(0.35);
    }

    [Fact]
    public async Task DecideNextActionAsync_NextActionParseFailure_ReturnsAskUser()
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>() // Empty answers
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var model = CreateModel();
        var decision = await model.DecideNextActionAsync("task", CreateTarget(), Array.Empty<AccessibilityElement>(), Array.Empty<string>());

        decision.Operation.Should().Be(AgentOperation.AskUser);
        decision.Reason.Should().Contain("Could not parse");
    }

    [Fact]
    public async Task DecideNextActionAsync_OpenAppAction_ReturnsOpenAppDecision()
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["nextAction"] = CreateChoiceAnswer("open_app:spotify", 0.95)
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var model = CreateModel();
        var decision = await model.DecideNextActionAsync("open spotify", CreateTarget(), Array.Empty<AccessibilityElement>(), Array.Empty<string>());

        decision.Operation.Should().Be(AgentOperation.OpenApp);
        decision.TargetId.Should().Be("spotify");
        decision.Confidence.Should().Be(0.95);
    }

    [Fact]
    public async Task DecideNextActionAsync_OpenUrlAction_ReturnsOpenUrlDecision()
    {
        string url = "https://github.com/harshitthek/HolyHand";
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["nextAction"] = CreateChoiceAnswer($"open_url:{url}", 0.98)
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var model = CreateModel();
        var decision = await model.DecideNextActionAsync($"open {url}", CreateTarget(), Array.Empty<AccessibilityElement>(), Array.Empty<string>());

        decision.Operation.Should().Be(AgentOperation.OpenUrl);
        decision.TargetId.Should().Be(url);
        decision.TextValue.Should().Be(url);
        decision.Confidence.Should().Be(0.98);
    }

    [Fact]
    public async Task DecideNextActionAsync_ClickAction_FindsElementAndReturnsClickDecision()
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["nextAction"] = CreateChoiceAnswer("click:e2", 0.91)
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var elements = new List<AccessibilityElement>
        {
            new() { Id = "e1", Role = "Button", Label = "Cancel" },
            new() { Id = "e2", Role = "Button", Label = "Confirm Order" }
        };

        var model = CreateModel();
        var decision = await model.DecideNextActionAsync("confirm", CreateTarget(), elements, Array.Empty<string>());

        decision.Operation.Should().Be(AgentOperation.Click);
        decision.TargetId.Should().Be("e2");
        decision.TargetLabel.Should().Be("Confirm Order");
        decision.Confidence.Should().Be(0.91);
    }

    [Fact]
    public async Task DecideNextActionAsync_TypeAndEnterAction_ReturnsTypeAndEnterDecision()
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["nextAction"] = CreateChoiceAnswer("type_and_enter:txtSearch:Adele", 0.88)
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var elements = new List<AccessibilityElement>
        {
            new() { Id = "txtSearch", Role = "Edit", Label = "Search query" }
        };

        var model = CreateModel();
        var decision = await model.DecideNextActionAsync("search Adele", CreateTarget(), elements, Array.Empty<string>());

        decision.Operation.Should().Be(AgentOperation.TypeAndEnter);
        decision.TargetId.Should().Be("txtSearch");
        decision.TargetLabel.Should().Be("Search query");
        decision.TextValue.Should().Be("Adele");
        decision.Confidence.Should().Be(0.88);
    }

    [Fact]
    public async Task DecideNextActionAsync_TypeTextAction_ReturnsTypeTextDecision()
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["nextAction"] = CreateChoiceAnswer("type:txtNote:Hello World", 0.84)
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var elements = new List<AccessibilityElement>
        {
            new() { Id = "txtNote", Role = "Edit", Label = "Note Content" }
        };

        var model = CreateModel();
        var decision = await model.DecideNextActionAsync("type Hello World", CreateTarget(), elements, Array.Empty<string>());

        decision.Operation.Should().Be(AgentOperation.TypeText);
        decision.TargetId.Should().Be("txtNote");
        decision.TextValue.Should().Be("Hello World");
    }

    [Theory]
    [InlineData("press:enter", AgentOperation.PressReturn)]
    [InlineData("press:space", AgentOperation.PressSpace)]
    [InlineData("press:tab", AgentOperation.PressTab)]
    [InlineData("press:escape", AgentOperation.PressEscape)]
    [InlineData("press:media_play", AgentOperation.PressMediaPlay)]
    public async Task DecideNextActionAsync_KeyPressActions_ReturnsCorrectKeyDecisions(string choice, AgentOperation expectedOp)
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["nextAction"] = CreateChoiceAnswer(choice, 0.85)
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var model = CreateModel();
        var decision = await model.DecideNextActionAsync("press key", CreateTarget(), Array.Empty<AccessibilityElement>(), Array.Empty<string>());

        decision.Operation.Should().Be(expectedOp);
    }

    [Theory]
    [InlineData("scroll:down", AgentOperation.ScrollDown)]
    [InlineData("scroll:up", AgentOperation.ScrollUp)]
    [InlineData("wait", AgentOperation.Wait)]
    [InlineData("done", AgentOperation.Done)]
    [InlineData("ask_user", AgentOperation.AskUser)]
    public async Task DecideNextActionAsync_StandardControlActions_ReturnsExpectedOperation(string choice, AgentOperation expectedOp)
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["nextAction"] = CreateChoiceAnswer(choice, 0.80)
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var model = CreateModel();
        var decision = await model.DecideNextActionAsync("navigate", CreateTarget(), Array.Empty<AccessibilityElement>(), Array.Empty<string>());

        decision.Operation.Should().Be(expectedOp);
    }

    [Fact]
    public async Task VerifyCompletionAsync_WhenDoneAndAboveThreshold_ReturnsTrue()
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["done"] = CreateBooleanAnswer(0.89)
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var model = CreateModel();
        var verified = await model.VerifyCompletionAsync("open notepad", CreateTarget(), Array.Empty<AccessibilityElement>(), Array.Empty<string>());

        verified.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyCompletionAsync_WhenDoneBelowThreshold_ReturnsFalse()
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["done"] = CreateBooleanAnswer(0.40) // below 0.50
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var model = CreateModel();
        var verified = await model.VerifyCompletionAsync("open notepad", CreateTarget(), Array.Empty<AccessibilityElement>(), Array.Empty<string>());

        verified.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyCompletionAsync_WhenAnswerIsFalse_ReturnsFalse()
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["done"] = CreateBooleanAnswer(0.15)
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var model = CreateModel();
        var verified = await model.VerifyCompletionAsync("open notepad", CreateTarget(), Array.Empty<AccessibilityElement>(), Array.Empty<string>());

        verified.Should().BeFalse();
    }

    [Theory]
    [InlineData(AgentOperation.Done)]
    [InlineData(AgentOperation.AskUser)]
    [InlineData(AgentOperation.Wait)]
    public async Task EvaluateActionRiskAsync_HarmlessOperations_ReturnsHarmlessWithoutApiCall(AgentOperation op)
    {
        var model = CreateModel();
        var decision = new AgentDecision { Operation = op };

        var risk = await model.EvaluateActionRiskAsync("goal", CreateTarget(), decision, null);

        risk.Should().Be(ActionRiskScore.Harmless);
        _mockClient.Verify(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateActionRiskAsync_ScoreThreeOrHigher_ReturnsIrreversibleOrExternalEffect()
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["actionRisk"] = CreateScoreAnswer(3, new List<double> { 0.05, 0.15, 0.80 })
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var model = CreateModel();
        var decision = new AgentDecision { Operation = AgentOperation.Click, TargetId = "btnDelete" };

        var risk = await model.EvaluateActionRiskAsync("delete file", CreateTarget(), decision, null);

        risk.Should().Be(ActionRiskScore.IrreversibleOrExternalEffect);
    }

    [Fact]
    public async Task EvaluateActionRiskAsync_ScoreTwoWithHighExternalProbability_ReturnsIrreversibleOrExternalEffect()
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["actionRisk"] = CreateScoreAnswer(2, new List<double> { 0.1, 0.35, 0.55 }) // prob[2] >= 0.5
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var model = CreateModel();
        var decision = new AgentDecision { Operation = AgentOperation.Click, TargetId = "btnPay" };

        var risk = await model.EvaluateActionRiskAsync("submit payment", CreateTarget(), decision, null);

        risk.Should().Be(ActionRiskScore.IrreversibleOrExternalEffect);
    }

    [Fact]
    public async Task EvaluateActionRiskAsync_ScoreOneOrTwo_ReturnsReversibleEdit()
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["actionRisk"] = CreateScoreAnswer(1, new List<double> { 0.2, 0.7, 0.1 })
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var model = CreateModel();
        var decision = new AgentDecision { Operation = AgentOperation.TypeText, TargetId = "txtTitle" };

        var risk = await model.EvaluateActionRiskAsync("edit title", CreateTarget(), decision, null);

        risk.Should().Be(ActionRiskScore.ReversibleEdit);
    }

    [Fact]
    public async Task EvaluateActionRiskAsync_WhenJevThrowsException_DefaultsToReversibleEdit()
    {
        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network failure"));

        var model = CreateModel();
        var decision = new AgentDecision { Operation = AgentOperation.Click, TargetId = "btnAction" };

        var risk = await model.EvaluateActionRiskAsync("click button", CreateTarget(), decision, null);

        risk.Should().Be(ActionRiskScore.ReversibleEdit);
    }

    [Theory]
    [InlineData("hi")]
    [InlineData("hello")]
    [InlineData("bark")]
    [InlineData("help")]
    public async Task DecideNextActionAsync_ConversationalGreeting_ReturnsAskUserWithGuidance(string greeting)
    {
        var model = CreateModel();
        var decision = await model.DecideNextActionAsync(greeting, CreateTarget(), Array.Empty<AccessibilityElement>(), Array.Empty<string>());

        decision.Operation.Should().Be(AgentOperation.AskUser);
        decision.Reason.Should().Contain("HolyHand automates");
    }

    [Fact]
    public async Task DecideNextActionAsync_VolumeCheck_ReturnsCheckVolumeDecision()
    {
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["nextAction"] = CreateChoiceAnswer("volume:check", 0.88)
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var model = CreateModel();
        var decision = await model.DecideNextActionAsync("check system volume", CreateTarget(), Array.Empty<AccessibilityElement>(), Array.Empty<string>());

        decision.Operation.Should().Be(AgentOperation.CheckVolume);
        decision.Reason.Should().Contain("volume");
    }

    [Theory]
    [InlineData("open fx sound", true)]
    [InlineData("open fxsound", true)]
    [InlineData("launch notepad", true)]
    [InlineData("start calculator", true)]
    [InlineData("please open spotify", true)]
    [InlineData("switch to discord", true)]
    [InlineData("open fx sound and click play", false)]
    [InlineData("open notepad and type hello", false)]
    [InlineData("check system volume", false)]
    [InlineData("click button", false)]
    public void IsPureLaunchGoal_CorrectlyClassifiesPhrases(string phrase, bool expected)
    {
        JevDecisionModel.IsPureLaunchGoal(phrase).Should().Be(expected);
    }

    [Theory]
    [InlineData("check system volume", true)]
    [InlineData("volume up", true)]
    [InlineData("volume down", true)]
    [InlineData("mute", true)]
    [InlineData("unmute", true)]
    [InlineData("louder", true)]
    [InlineData("quieter", true)]
    [InlineData("open notepad", false)]
    [InlineData("click settings", false)]
    public void IsVolumeGoal_CorrectlyClassifiesPhrases(string phrase, bool expected)
    {
        JevDecisionModel.IsVolumeGoal(phrase).Should().Be(expected);
    }

    [Fact]
    public async Task DecideNextActionAsync_PureLaunchGoal_OmitsBackgroundUIElementsFromCandidates()
    {
        EvaluateRequest? capturedRequest = null;
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["nextAction"] = CreateChoiceAnswer("open_app:fx sound", 0.99)
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .Callback<EvaluateRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(response);

        var elements = new List<AccessibilityElement>
        {
            new() { Id = "e1", Role = "Button", Label = "Chat Send", Enabled = true },
            new() { Id = "e2", Role = "Edit", Label = "Prompt Input", Enabled = true }
        };

        var model = CreateModel();
        var decision = await model.DecideNextActionAsync("open fx sound", CreateTarget(), elements, Array.Empty<string>());

        decision.Operation.Should().Be(AgentOperation.OpenApp);
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Questions.Should().ContainKey("nextAction");
        var choices = capturedRequest.Questions["nextAction"].Criteria as Dictionary<string, string>;
        choices.Should().NotBeNull();
        choices!.Should().ContainKey("open_app:fx sound");
        choices.Should().NotContainKey("click:e1");
        choices.Should().NotContainKey("type:e2:fx sound");
    }

    [Fact]
    public async Task DecideNextActionAsync_VolumeGoal_OmitsBackgroundUIElementsFromCandidates()
    {
        EvaluateRequest? capturedRequest = null;
        var response = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["nextAction"] = CreateChoiceAnswer("volume:check", 0.95)
            }
        };

        _mockClient.Setup(c => c.EvaluateAsync(It.IsAny<EvaluateRequest>(), It.IsAny<CancellationToken>()))
            .Callback<EvaluateRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(response);

        var elements = new List<AccessibilityElement>
        {
            new() { Id = "e1", Role = "Button", Label = "Discord Channel", Enabled = true }
        };

        var model = CreateModel();
        var decision = await model.DecideNextActionAsync("check system volume", CreateTarget(), elements, Array.Empty<string>());

        decision.Operation.Should().Be(AgentOperation.CheckVolume);
        capturedRequest.Should().NotBeNull();
        var choices = capturedRequest!.Questions["nextAction"].Criteria as Dictionary<string, string>;
        choices.Should().NotBeNull();
        choices!.Should().ContainKey("volume:check");
        choices.Should().NotContainKey("click:e1");
    }
}


