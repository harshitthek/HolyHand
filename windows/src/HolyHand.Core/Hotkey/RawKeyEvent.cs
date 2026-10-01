namespace HolyHand.Core.Hotkey;

/// <summary>
/// A platform-independent raw keyboard event received from a low-level hook.
/// </summary>
public readonly record struct RawKeyEvent
{
    public const int VkControl = 0x11;
    public const int VkLControl = 0xA2;
    public const int VkRControl = 0xA3;
    public const int VkLWin = 0x5B;
    public const int VkRWin = 0x5C;
    public const int VkDummySuppression = 0xE8;

    public int KeyCode { get; init; }
    public bool IsKeyUp { get; init; }
    public bool IsInjected { get; init; }
    public long TimestampMs { get; init; }

    public bool IsCtrl => KeyCode is VkControl or VkLControl or VkRControl;
    public bool IsWin => KeyCode is VkLWin or VkRWin;
    public bool IsModifier => IsCtrl || IsWin;

    public static RawKeyEvent KeyDown(int keyCode, bool isInjected = false, long timestamp = 0) =>
        new() { KeyCode = keyCode, IsKeyUp = false, IsInjected = isInjected, TimestampMs = timestamp };

    public static RawKeyEvent KeyUp(int keyCode, bool isInjected = false, long timestamp = 0) =>
        new() { KeyCode = keyCode, IsKeyUp = true, IsInjected = isInjected, TimestampMs = timestamp };
}
