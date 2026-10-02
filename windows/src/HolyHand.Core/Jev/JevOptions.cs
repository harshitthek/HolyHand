namespace HolyHand.Core.Jev;

public class JevOptions
{
    public const string DefaultVercelGatewayBaseUrl = "https://ai-gateway.vercel.sh";
    public const string DefaultTypeSafeBaseUrl = "https://api.typesafe.ai";

    public const string DefaultVercelModelId = "typesafe-ai/jev";
    public const string DefaultTypeSafeModelId = "jev-latest";

    public string BaseUrl { get; set; } = DefaultVercelGatewayBaseUrl;
    public string ModelId { get; set; } = DefaultVercelModelId;
    public string? ApiKey { get; set; }
    public bool ZeroDataRetention { get; set; } = false;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 4;
    public double DecisionConfidenceThreshold { get; set; } = 0.50;
    public double RiskConfidenceThreshold { get; set; } = 0.70;

    public bool IsNativeTypeSafe =>
        BaseUrl.Contains("typesafe.ai", StringComparison.OrdinalIgnoreCase) ||
        ApiKey?.StartsWith("apikey_", StringComparison.OrdinalIgnoreCase) == true;

    public static JevOptions FromEnvironment()
    {
        var options = new JevOptions();

        var key = Environment.GetEnvironmentVariable("AI_GATEWAY_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
            key = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
            key = Environment.GetEnvironmentVariable("JEV_API_KEY");

        if (!string.IsNullOrWhiteSpace(key))
            options.ApiKey = key;

        var baseUrl = Environment.GetEnvironmentVariable("AI_GATEWAY_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = Environment.GetEnvironmentVariable("TYPESAFE_BASE_URL");

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            options.BaseUrl = baseUrl;
        }
        else if (options.ApiKey?.StartsWith("apikey_", StringComparison.OrdinalIgnoreCase) == true)
        {
            options.BaseUrl = DefaultTypeSafeBaseUrl;
        }

        var model = Environment.GetEnvironmentVariable("JEV_MODEL");
        if (!string.IsNullOrWhiteSpace(model))
        {
            options.ModelId = model;
        }
        else if (options.IsNativeTypeSafe)
        {
            options.ModelId = DefaultTypeSafeModelId;
        }

        if (bool.TryParse(Environment.GetEnvironmentVariable("ZERO_DATA_RETENTION"), out var zeroRetention))
            options.ZeroDataRetention = zeroRetention;

        if (double.TryParse(Environment.GetEnvironmentVariable("DECISION_CONFIDENCE_THRESHOLD"), out var conf))
            options.DecisionConfidenceThreshold = conf;

        if (double.TryParse(Environment.GetEnvironmentVariable("RISK_CONFIDENCE_THRESHOLD"), out var riskConf))
            options.RiskConfidenceThreshold = riskConf;

        return options;
    }
}
