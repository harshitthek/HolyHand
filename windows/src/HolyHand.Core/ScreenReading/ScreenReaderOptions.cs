namespace HolyHand.Core.ScreenReading;

public record ScreenReaderOptions
{
    public int MaxNodes { get; init; } = 500;
    public int MaxDepth { get; init; } = 30;
    public int MaxCandidates { get; init; } = 40;
    public int OcrFallbackThreshold { get; init; } = 0;
    public bool FilterOffscreen { get; init; } = true;
    public bool FilterDisabled { get; init; } = false;

    public static ScreenReaderOptions Default => new();
}
