using System.Text.Json;
using System.Text.Json.Serialization;

namespace HolyHand.Core.Jev;

public record EvaluateRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("state")]
    public required object State { get; init; }

    [JsonPropertyName("questions")]
    public required Dictionary<string, QuestionDefinition> Questions { get; init; }

    [JsonPropertyName("providerOptions")]
    public GatewayProviderOptions? ProviderOptions { get; init; }
}

public record QuestionDefinition
{
    [JsonPropertyName("type")]
    public required string Type { get; init; } // "boolean", "choice", "score"

    [JsonPropertyName("instructions")]
    public string? Instructions { get; init; }

    [JsonPropertyName("criteria")]
    public object? Criteria { get; init; } // Dictionary<string, string> for choice, List<string> for score

    public static QuestionDefinition Boolean(string instructions) => new()
    {
        Type = "boolean",
        Instructions = instructions
    };

    public static QuestionDefinition Choice(Dictionary<string, string> criteria, string? instructions = null) => new()
    {
        Type = "choice",
        Instructions = instructions,
        Criteria = criteria
    };

    public static QuestionDefinition Score(IReadOnlyList<string> orderedCriteria, string? instructions = null) => new()
    {
        Type = "score",
        Instructions = instructions,
        Criteria = orderedCriteria
    };
}

public record GatewayProviderOptions
{
    [JsonPropertyName("gateway")]
    public GatewayOptions? Gateway { get; init; }
}

public record GatewayOptions
{
    [JsonPropertyName("zeroDataRetention")]
    public bool? ZeroDataRetention { get; init; }

    [JsonPropertyName("only")]
    public List<string>? Only { get; init; }
}

public record EvaluateResponse
{
    [JsonPropertyName("answers")]
    public Dictionary<string, JsonElement> Answers { get; init; } = new();

    [JsonPropertyName("usage")]
    public UsageInfo? Usage { get; init; }

    [JsonPropertyName("providerMetadata")]
    public ProviderMetadataInfo? ProviderMetadata { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }

    public bool TryGetBooleanAnswer(string questionName, out double probability, out bool isTrue)
    {
        probability = 0;
        isTrue = false;
        if (!Answers.TryGetValue(questionName, out var element))
            return false;

        if (element.TryGetProperty("probability", out var probProp) && probProp.TryGetDouble(out var p))
        {
            probability = p;
            isTrue = p >= 0.5;
            return true;
        }
        return false;
    }

    public bool TryGetChoiceAnswer(string questionName, out string choice, out double confidence, out Dictionary<string, double> probabilities)
    {
        choice = string.Empty;
        confidence = 0;
        probabilities = new Dictionary<string, double>();

        if (!Answers.TryGetValue(questionName, out var element))
            return false;

        if (element.TryGetProperty("choice", out var choiceProp))
        {
            choice = choiceProp.GetString() ?? string.Empty;
        }

        if (element.TryGetProperty("probabilities", out var probsProp) && probsProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in probsProp.EnumerateObject())
            {
                if (prop.Value.TryGetDouble(out var p))
                {
                    probabilities[prop.Name] = p;
                }
            }
        }

        if (!string.IsNullOrEmpty(choice) && probabilities.TryGetValue(choice, out var topProb))
        {
            confidence = topProb;
        }

        return !string.IsNullOrEmpty(choice);
    }

    public bool TryGetScoreAnswer(string questionName, out int score, out List<double> probabilities)
    {
        score = 0;
        probabilities = new List<double>();

        if (!Answers.TryGetValue(questionName, out var element))
            return false;

        if (element.TryGetProperty("score", out var scoreProp))
        {
            if (scoreProp.ValueKind == JsonValueKind.Number)
            {
                if (scoreProp.TryGetInt32(out var s))
                    score = s;
                else if (scoreProp.TryGetDouble(out var d))
                    score = (int)Math.Round(d);
            }
            else if (scoreProp.ValueKind == JsonValueKind.String && int.TryParse(scoreProp.GetString(), out var s))
            {
                score = s;
            }
        }

        if (element.TryGetProperty("probabilities", out var probsProp) && probsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in probsProp.EnumerateArray())
            {
                if (item.TryGetDouble(out var p))
                {
                    probabilities.Add(p);
                }
            }
        }

        return true;
    }
}

public record UsageInfo
{
    [JsonPropertyName("promptTokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completionTokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("totalTokens")]
    public int TotalTokens { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

public record ProviderMetadataInfo
{
    [JsonPropertyName("gateway")]
    public GatewayMetadata? Gateway { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

public record GatewayMetadata
{
    [JsonPropertyName("cost")]
    public JsonElement? RawCost { get; init; }

    [JsonIgnore]
    public double? Cost
    {
        get
        {
            if (RawCost == null || RawCost.Value.ValueKind == JsonValueKind.Null || RawCost.Value.ValueKind == JsonValueKind.Undefined)
                return null;
            if (RawCost.Value.ValueKind == JsonValueKind.Number && RawCost.Value.TryGetDouble(out var d))
                return d;
            if (RawCost.Value.ValueKind == JsonValueKind.String && double.TryParse(RawCost.Value.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            return null;
        }
        init
        {
            if (value.HasValue)
            {
                RawCost = JsonSerializer.SerializeToElement(value.Value);
            }
        }
    }

    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("generationId")]
    public string? GenerationId { get; init; }

    [JsonPropertyName("routing")]
    public JsonElement? Routing { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
