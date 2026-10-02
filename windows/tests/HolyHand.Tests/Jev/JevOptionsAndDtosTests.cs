using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using HolyHand.Core.Jev;
using Xunit;

namespace HolyHand.Tests.Jev;

public class JevOptionsAndDtosTests : IDisposable
{
    private readonly List<string> _envKeysToClean = new();

    public void Dispose()
    {
        foreach (var key in _envKeysToClean)
        {
            Environment.SetEnvironmentVariable(key, null, EnvironmentVariableTarget.Process);
        }
    }

    private void SetEnv(string key, string? val)
    {
        _envKeysToClean.Add(key);
        Environment.SetEnvironmentVariable(key, val, EnvironmentVariableTarget.Process);
    }

    [Fact]
    public void FromEnvironment_Defaults_UsesVercelGatewayDefaults()
    {
        SetEnv("AI_GATEWAY_API_KEY", null);
        SetEnv("TYPESAFE_API_KEY", null);
        SetEnv("JEV_API_KEY", null);
        SetEnv("AI_GATEWAY_BASE_URL", null);
        SetEnv("TYPESAFE_BASE_URL", null);
        SetEnv("JEV_MODEL", null);

        var options = JevOptions.FromEnvironment();

        options.BaseUrl.Should().Be(JevOptions.DefaultVercelGatewayBaseUrl);
        options.ModelId.Should().Be(JevOptions.DefaultVercelModelId);
        options.DecisionConfidenceThreshold.Should().Be(0.50);
        options.RiskConfidenceThreshold.Should().Be(0.70);
        options.IsNativeTypeSafe.Should().BeFalse();
    }

    [Fact]
    public void FromEnvironment_NativeApiKeyPrefix_AutoSwitchesToNativeTypeSafe()
    {
        SetEnv("AI_GATEWAY_API_KEY", null);
        SetEnv("TYPESAFE_API_KEY", "apikey_1234567890abcdef_secret");

        var options = JevOptions.FromEnvironment();

        options.ApiKey.Should().Be("apikey_1234567890abcdef_secret");
        options.IsNativeTypeSafe.Should().BeTrue();
        options.BaseUrl.Should().Be(JevOptions.DefaultTypeSafeBaseUrl);
        options.ModelId.Should().Be(JevOptions.DefaultTypeSafeModelId);
    }

    [Fact]
    public void FromEnvironment_CustomBaseUrlAndModel_HonorsOverrides()
    {
        SetEnv("AI_GATEWAY_BASE_URL", "https://custom.gateway.internal");
        SetEnv("JEV_MODEL", "custom-jev-model");
        SetEnv("ZERO_DATA_RETENTION", "true");
        SetEnv("DECISION_CONFIDENCE_THRESHOLD", "0.65");
        SetEnv("RISK_CONFIDENCE_THRESHOLD", "0.85");

        var options = JevOptions.FromEnvironment();

        options.BaseUrl.Should().Be("https://custom.gateway.internal");
        options.ModelId.Should().Be("custom-jev-model");
        options.ZeroDataRetention.Should().BeTrue();
        options.DecisionConfidenceThreshold.Should().Be(0.65);
        options.RiskConfidenceThreshold.Should().Be(0.85);
    }

    [Fact]
    public void UsageInfo_EffectiveTokens_ComputesCorrectlyAcrossProviders()
    {
        // Provider A: promptTokens / completionTokens
        var usageA = new UsageInfo
        {
            PromptTokens = 120,
            CompletionTokens = 45,
            TotalTokens = 165
        };

        usageA.EffectivePromptTokens.Should().Be(120);
        usageA.EffectiveCompletionTokens.Should().Be(45);
        usageA.EffectiveTotalTokens.Should().Be(165);

        // Provider B: input_tokens / output_tokens (Native TypeSafe)
        var usageB = new UsageInfo
        {
            InputTokens = 250,
            OutputTokens = 35
        };

        usageB.EffectivePromptTokens.Should().Be(250);
        usageB.EffectiveCompletionTokens.Should().Be(35);
        usageB.EffectiveTotalTokens.Should().Be(285);
    }

    [Fact]
    public void GatewayMetadata_Cost_ParsesNumericAndStringRepresentations()
    {
        var metaNumeric = new GatewayMetadata
        {
            RawCost = JsonDocument.Parse("0.00042").RootElement
        };
        metaNumeric.Cost.Should().BeApproximately(0.00042, 0.000001);

        var metaString = new GatewayMetadata
        {
            RawCost = JsonDocument.Parse("\"0.00125\"").RootElement
        };
        metaString.Cost.Should().BeApproximately(0.00125, 0.000001);

        var metaNull = new GatewayMetadata { RawCost = null };
        metaNull.Cost.Should().BeNull();
    }

    [Fact]
    public void QuestionDefinition_Factories_GenerateCorrectSchemas()
    {
        var boolQ = QuestionDefinition.Boolean("Is this true?");
        boolQ.Type.Should().Be("boolean");
        boolQ.Instructions.Should().Be("Is this true?");

        var choices = new Dictionary<string, string> { ["c1"] = "Option 1", ["c2"] = "Option 2" };
        var choiceQ = QuestionDefinition.Choice(choices, "Pick one");
        choiceQ.Type.Should().Be("choice");
        choiceQ.Instructions.Should().Be("Pick one");
        choiceQ.Criteria.Should().BeEquivalentTo(choices);

        var scoreOptions = new List<string> { "low", "medium", "high" };
        var scoreQ = QuestionDefinition.Score(scoreOptions, "Rate risk");
        scoreQ.Type.Should().Be("score");
        scoreQ.Instructions.Should().Be("Rate risk");
        scoreQ.Criteria.Should().BeEquivalentTo(scoreOptions);
    }

    [Fact]
    public void EvaluateResponse_TryGetBooleanAnswer_HandlesNativeNoulFormat()
    {
        var doc = JsonDocument.Parse("{\"testQ\": {\"type\": \"noul\", \"noul\": 0.88}}");
        var resp = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["testQ"] = doc.RootElement.GetProperty("testQ")
            }
        };

        bool ok = resp.TryGetBooleanAnswer("testQ", out var prob, out var isTrue);

        ok.Should().BeTrue();
        prob.Should().Be(0.88);
        isTrue.Should().BeTrue();
    }

    [Fact]
    public void EvaluateResponse_TryGetScoreAnswer_HandlesArrayProbabilities()
    {
        var doc = JsonDocument.Parse("{\"riskQ\": {\"score\": 2, \"probabilities\": [0.1, 0.6, 0.3]}}");
        var resp = new EvaluateResponse
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["riskQ"] = doc.RootElement.GetProperty("riskQ")
            }
        };

        bool ok = resp.TryGetScoreAnswer("riskQ", out var score, out var probs);

        ok.Should().BeTrue();
        score.Should().Be(2);
        probs.Should().Equal(0.1, 0.6, 0.3);
    }
}
