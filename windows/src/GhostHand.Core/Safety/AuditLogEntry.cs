using System.Text.Json.Serialization;
using HolyHand.Core.Models;

namespace HolyHand.Core.Safety;

public record AuditLogEntry
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("goal")]
    public string Goal { get; init; } = string.Empty;

    [JsonPropertyName("operation")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AgentOperation Operation { get; init; }

    [JsonPropertyName("targetId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetId { get; init; }

    [JsonPropertyName("targetLabel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetLabel { get; init; }

    [JsonPropertyName("targetRole")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetRole { get; init; }

    [JsonPropertyName("appProcess")]
    public string AppProcess { get; init; } = string.Empty;

    [JsonPropertyName("appTitle")]
    public string AppTitle { get; init; } = string.Empty;

    [JsonPropertyName("decisionType")]
    public string DecisionType { get; init; } = "auto"; // "auto", "confirmed", "rejected", "denied"

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }
}
