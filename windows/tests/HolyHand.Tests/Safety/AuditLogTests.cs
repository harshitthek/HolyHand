using System.IO;
using System.Text.Json;
using FluentAssertions;
using HolyHand.Core.Models;
using HolyHand.Core.Safety;
using HolyHand.Platform.Safety;
using Xunit;

namespace HolyHand.Tests.Safety;

public class AuditLogTests : IDisposable
{
    private readonly string _tempAuditDir;

    public AuditLogTests()
    {
        _tempAuditDir = Path.Combine(Path.GetTempPath(), "HolyHand_AuditTests_" + Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public async Task AuditLog_WritesValidJsonl_WithAllFields()
    {
        using var auditLog = new JsonlAuditLog(_tempAuditDir);

        var entry = new AuditLogEntry
        {
            Goal = "Search for quarterly reports",
            Operation = AgentOperation.Click,
            TargetId = "e12",
            TargetLabel = "Download PDF",
            TargetRole = "Button",
            AppProcess = "explorer",
            AppTitle = "Documents Folder",
            DecisionType = "auto",
            Reason = "Benign navigation action"
        };

        await auditLog.LogAsync(entry);

        var logFiles = Directory.GetFiles(_tempAuditDir, "*.jsonl");
        logFiles.Should().HaveCount(1);

        var lines = await File.ReadAllLinesAsync(logFiles[0]);
        lines.Should().HaveCount(1);

        using var doc = JsonDocument.Parse(lines[0]);
        var root = doc.RootElement;

        root.GetProperty("goal").GetString().Should().Be("Search for quarterly reports");
        root.GetProperty("operation").GetString().Should().Be("Click");
        root.GetProperty("targetId").GetString().Should().Be("e12");
        root.GetProperty("targetLabel").GetString().Should().Be("Download PDF");
        root.GetProperty("appProcess").GetString().Should().Be("explorer");
        root.GetProperty("decisionType").GetString().Should().Be("auto");
    }

    [Fact]
    public async Task AuditLog_SanitizesSensitiveData_NeverWritesRawSecrets()
    {
        using var auditLog = new JsonlAuditLog(_tempAuditDir);

        var sensitiveEntry = new AuditLogEntry
        {
            Goal = "Use card 4532 0150 1234 5678 to purchase license vck_live_secret_key_abcdefg12345",
            Operation = AgentOperation.TypeText,
            TargetId = "e5",
            TargetLabel = "Card number 4532-0150-1234-5678",
            TargetRole = "Edit",
            AppProcess = "edge",
            AppTitle = "Checkout Portal",
            DecisionType = "confirmed",
            Reason = "User approved payment with key vck_live_key_9999"
        };

        await auditLog.LogAsync(sensitiveEntry);

        var logFiles = Directory.GetFiles(_tempAuditDir, "*.jsonl");
        var content = await File.ReadAllTextAsync(logFiles[0]);

        // Raw credit cards and API keys must be scrubbed
        content.Should().NotContain("4532 0150 1234 5678");
        content.Should().NotContain("vck_live_secret_key");
        content.Should().NotContain("vck_live_key");

        // Redacted tokens must be present
        content.Should().Contain("[REDACTED_CARD]");
        content.Should().Contain("[REDACTED_KEY]");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempAuditDir))
            {
                Directory.Delete(_tempAuditDir, recursive: true);
            }
        }
        catch { }
    }
}
