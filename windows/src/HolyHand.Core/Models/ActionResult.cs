namespace HolyHand.Core.Models;

public record ActionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Message { get; init; }
    public string? Error => ErrorMessage;
    public TimeSpan Duration { get; init; }

    public AppTarget? NewTarget { get; init; }

    public static ActionResult Succeeded(TimeSpan duration = default) => new() { Success = true, Duration = duration };
    public static ActionResult Failed(string error, TimeSpan duration = default) => new() { Success = false, ErrorMessage = error, Duration = duration };
    public static ActionResult SuccessResult(string? message = null) => new() { Success = true, Message = message };
    public static ActionResult FailureResult(string error) => new() { Success = false, ErrorMessage = error };
    public static ActionResult TargetChanged(AppTarget target, string? message = null) => new() { Success = true, Message = message, NewTarget = target };
}
