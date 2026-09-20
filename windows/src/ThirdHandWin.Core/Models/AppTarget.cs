using System.Drawing;

namespace HolyHand.Core.Models;

/// <summary>
/// Information about the foreground / target application and its active window.
/// </summary>
public record AppTarget
{
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public string ExecutablePath { get; init; } = string.Empty;
    public string WindowTitle { get; init; } = string.Empty;
    public IntPtr WindowHandle { get; init; }
    public Rectangle WindowBounds { get; init; }
    public bool IsElevated { get; init; }
}
