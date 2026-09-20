using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using HolyHand.Core.Hotkey;
using HolyHand.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace HolyHand.Platform.Hotkey;

public class LowLevelKeyboardHook : IHotkeyService
{
    private const uint WmQuit = 0x0012;
    private const uint WmKeyup = 0x0101;
    private const uint WmSyskeyup = 0x0105;

    private readonly ILogger<LowLevelKeyboardHook> _logger;
    private readonly ChordStateMachine _stateMachine;
    private readonly Channel<RawKeyEvent> _keyChannel;

    private Thread? _hookThread;
    private uint _hookThreadId;
    private HHOOK _hHook = HHOOK.Null;
    private HOOKPROC? _hookProc;
    private readonly CancellationTokenSource _cts = new();

    public event EventHandler? HotkeyPressed;
    public event EventHandler? KillSwitchTriggered;

    public ChordStateMachine StateMachine => _stateMachine;

    public LowLevelKeyboardHook(ILogger<LowLevelKeyboardHook> logger)
    {
        _logger = logger;
        _stateMachine = new ChordStateMachine();
        _keyChannel = Channel.CreateUnbounded<RawKeyEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _stateMachine.OnTrigger += () => HotkeyPressed?.Invoke(this, EventArgs.Empty);
        _stateMachine.OnCancel += () => KillSwitchTriggered?.Invoke(this, EventArgs.Empty);

        // Start processing channel events asynchronously
        Task.Run(ProcessKeyChannelAsync);
    }

    public void Start()
    {
        if (_hookThread != null) return;

        var tcs = new TaskCompletionSource();
        _hookThread = new Thread(() => HookThreadMain(tcs))
        {
            IsBackground = true,
            Name = "HolyHand.KeyboardHookThread"
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();

        tcs.Task.Wait(TimeSpan.FromSeconds(3));
        _logger.LogInformation("Low-level keyboard hook started on dedicated thread ID {ThreadId}", _hookThreadId);
    }

    public void Stop()
    {
        _cts.Cancel();
        if (_hookThreadId != 0)
        {
            PInvoke.PostThreadMessage(_hookThreadId, WmQuit, new WPARAM(0), new LPARAM(0));
            _hookThread?.Join(TimeSpan.FromSeconds(2));
            _hookThread = null;
            _hookThreadId = 0;
        }
        _logger.LogInformation("Low-level keyboard hook stopped.");
    }

    private void HookThreadMain(TaskCompletionSource tcs)
    {
        _hookThreadId = GetCurrentWin32ThreadId();
        _hookProc = HookCallback;

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        var hMod = (HINSTANCE)(curModule?.BaseAddress ?? IntPtr.Zero);

        _hHook = PInvoke.SetWindowsHookEx(
            WINDOWS_HOOK_ID.WH_KEYBOARD_LL,
            _hookProc,
            hMod,
            0);

        if (_hHook.IsNull)
        {
            _logger.LogError("Failed to install WH_KEYBOARD_LL hook. Error: {Error}", Marshal.GetLastWin32Error());
            tcs.TrySetResult();
            return;
        }

        tcs.TrySetResult();

        // Run message pump for the hook
        MSG msg;
        while (PInvoke.GetMessage(out msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        if (!_hHook.IsNull)
        {
            PInvoke.UnhookWindowsHookEx(_hHook);
            _hHook = HHOOK.Null;
        }
    }

    private LRESULT HookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0)
        {
            var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool isKeyUp = (wParam.Value == WmKeyup || wParam.Value == WmSyskeyup);
            bool isInjected = (kbd.flags & 0x10) != 0; // LLKHF_INJECTED = 0x10

            int vk = (int)kbd.vkCode;

            // Start menu suppression: if Win key is about to be released and Chord is active, inject dummy key 0xE8
            if (isKeyUp && (vk is RawKeyEvent.VkLWin or RawKeyEvent.VkRWin) && !isInjected)
            {
                if (_stateMachine.CurrentState.ChordArmed && !_stateMachine.CurrentState.Interrupted)
                {
                    SuppressStartMenu();
                }
            }

            // Keep hook callback tiny: write to channel and immediately return
            _keyChannel.Writer.TryWrite(new RawKeyEvent
            {
                KeyCode = vk,
                IsKeyUp = isKeyUp,
                IsInjected = isInjected,
                TimestampMs = kbd.time
            });
        }

        return PInvoke.CallNextHookEx(_hHook, nCode, wParam, lParam);
    }

    private static void SuppressStartMenu()
    {
        // Inject unassigned virtual key tap (VK 0xE8) to suppress Start menu
        unsafe
        {
            var inputs = stackalloc Windows.Win32.UI.Input.KeyboardAndMouse.INPUT[2];
            inputs[0].type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_KEYBOARD;
            inputs[0].Anonymous.ki.wVk = (Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY)RawKeyEvent.VkDummySuppression;
            inputs[0].Anonymous.ki.dwFlags = 0; // KeyDown

            inputs[1].type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_KEYBOARD;
            inputs[1].Anonymous.ki.wVk = (Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY)RawKeyEvent.VkDummySuppression;
            inputs[1].Anonymous.ki.dwFlags = Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

            PInvoke.SendInput(2, inputs, Marshal.SizeOf<Windows.Win32.UI.Input.KeyboardAndMouse.INPUT>());
        }
    }

    private async Task ProcessKeyChannelAsync()
    {
        var reader = _keyChannel.Reader;
        while (await reader.WaitToReadAsync(_cts.Token))
        {
            while (reader.TryRead(out var keyEvent))
            {
                _stateMachine.ProcessKeyEvent(keyEvent);
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    [DllImport("kernel32.dll", EntryPoint = "GetCurrentThreadId")]
    private static extern uint GetCurrentWin32ThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }
}
