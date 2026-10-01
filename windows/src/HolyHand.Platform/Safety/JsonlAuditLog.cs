using System.Text.Json;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Safety;
using HolyHand.Core.ScreenReading;
using Microsoft.Extensions.Logging;

namespace HolyHand.Platform.Safety;

public class JsonlAuditLog : IAuditLog, IDisposable
{
    private readonly string _auditDirectory;
    private readonly ILogger<JsonlAuditLog> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    public JsonlAuditLog(string? auditDirectory = null, ILogger<JsonlAuditLog>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<JsonlAuditLog>.Instance;

        _auditDirectory = auditDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HolyHand",
            "audit");

        try
        {
            Directory.CreateDirectory(_auditDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create audit log directory at '{Directory}'", _auditDirectory);
        }
    }

    public async Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        // Safety: scrub any potential secrets or sensitive tokens from audit text
        var sanitizedEntry = entry with
        {
            Goal = SecretSanitizer.Sanitize(entry.Goal, isPassword: false),
            TargetLabel = entry.TargetLabel != null ? SecretSanitizer.Sanitize(entry.TargetLabel, isPassword: false) : null,
            Reason = entry.Reason != null ? SecretSanitizer.Sanitize(entry.Reason, isPassword: false) : null,
            AppTitle = SecretSanitizer.Sanitize(entry.AppTitle, isPassword: false)
        };

        var dateString = sanitizedEntry.Timestamp.ToString("yyyy-MM-dd");
        var filePath = Path.Combine(_auditDirectory, $"audit-{dateString}.jsonl");
        var line = JsonSerializer.Serialize(sanitizedEntry) + Environment.NewLine;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(filePath, line, cancellationToken);
            _logger.LogDebug("Logged audit entry: {DecisionType} on {Operation} ({Target})",
                sanitizedEntry.DecisionType, sanitizedEntry.Operation, sanitizedEntry.TargetLabel ?? sanitizedEntry.TargetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log entry to '{File}'", filePath);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _lock.Dispose();
        }
    }
}
