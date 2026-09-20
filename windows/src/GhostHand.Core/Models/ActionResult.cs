namespace HolyHand.Core.Models;

public record ActionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }

    public static ActionResult Succeeded(TimeSpan duration = default) => new() { Success = true, Duration = duration };
    public static ActionResult Failed(string error, TimeSpan duration = default) => new() { Success = false, ErrorMessage = error, Duration = duration };
}
