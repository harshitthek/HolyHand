using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using HolyHand.App.Windows;
using HolyHand.Core.Agent;
using HolyHand.Core.Common;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Jev;
using HolyHand.Core.Safety;
using HolyHand.Core.ScreenReading;
using HolyHand.Platform.Audio;
using HolyHand.Platform.Execution;
using HolyHand.Platform.Hotkey;
using HolyHand.Platform.Launcher;
using HolyHand.Platform.Safety;
using HolyHand.Platform.ScreenReading;
using HolyHand.Platform.Speech;
using HolyHand.Platform.Windowing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;

namespace HolyHand.App;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private static EventWaitHandle? _activateEvent;
    private const string MutexName = "Global\\HolyHand_SingleInstance_Mutex_2026";
    private const string ActivateEventName = "Global\\HolyHand_Activate_Event_2026";
    private const int HotkeyIdCtrlShiftSpace = 0x4848;
    private const int HotkeyIdAltSpace = 0x4849;
    private const int HotkeyIdCtrlAltSpace = 0x484A;
    private const int HotkeyIdWinShiftH = 0x484B;
    private const int HotkeyIdCtrlShiftH = 0x484C;
    private ServiceProvider? _serviceProvider;

    private IHotkeyService? _hotkeyService;
    private IWindowCaptureService? _windowCapture;
    private PromptPopupWindow? _popup;
    private ConfirmationDialog? _confirmationDialog;
    private CancellationTokenSource? _runCts;
    private ILogger<App>? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load .env variables
        EnvLoader.Load();

        bool createdNew;
        _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);
        if (!createdNew)
        {
            try
            {
                using var evt = EventWaitHandle.OpenExisting(ActivateEventName);
                evt.Set();
            }
            catch { }
            Shutdown(0);
            return;
        }

        try
        {
            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
            Task.Run(() =>
            {
                while (true)
                {
                    _activateEvent.WaitOne();
                    Dispatcher.Invoke(() =>
                    {
                        var target = _windowCapture?.CaptureForegroundWindow();
                        _popup?.ShowForTarget(target);
                    });
                }
            });
        }
        catch { }

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        _logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        _windowCapture = _serviceProvider.GetRequiredService<IWindowCaptureService>();
        _hotkeyService = _serviceProvider.GetRequiredService<IHotkeyService>();

        // Check if API key is configured; if not, show first-run setup dialog
        var credentialStore = _serviceProvider.GetRequiredService<ICredentialStore>();
        if (!credentialStore.HasKey())
        {
            _logger.LogInformation("No API key found in environment or Credential Manager. Showing setup dialog.");
            var setupDialog = new ApiKeySetupDialog(credentialStore);
            setupDialog.ShowDialog();
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Pre-create windows (hidden at startup for instant display)
        _popup = new PromptPopupWindow();
        MainWindow = _popup;
        _popup.SpeechInput = _serviceProvider.GetService<ISpeechInput>();
        _popup.TaskSubmitted += OnTaskSubmitted;
        _popup.Cancelled += OnTaskCancelled;

        _confirmationDialog = new ConfirmationDialog();

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _hotkeyService.KillSwitchTriggered += OnKillSwitchTriggered;
        _hotkeyService.Start();

        // Register native hotkeys: Alt + Space, Ctrl + Shift + Space, Ctrl + Alt + Space, Win + Shift + H, Ctrl + Shift + H
        try
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(_popup);
            helper.EnsureHandle();
            var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(WndProc);

            System.Windows.Interop.ComponentDispatcher.ThreadFilterMessage += (ref System.Windows.Interop.MSG msg, ref bool handled) =>
            {
                const int WM_HOTKEY = 0x0312;
                if (msg.message == WM_HOTKEY)
                {
                    var id = msg.wParam.ToInt32();
                    if (id is HotkeyIdAltSpace or HotkeyIdCtrlShiftSpace or HotkeyIdCtrlAltSpace or HotkeyIdWinShiftH or HotkeyIdCtrlShiftH)
                    {
                        _logger?.LogInformation("ComponentDispatcher received WM_HOTKEY with id 0x{Id:X}", id);
                        OnHotkeyPressed(this, EventArgs.Empty);
                        handled = true;
                    }
                }
            };

            const uint MOD_ALT = 0x0001;
            const uint MOD_CONTROL = 0x0002;
            const uint MOD_SHIFT = 0x0004;
            const uint MOD_WIN = 0x0008;
            const uint MOD_NOREPEAT = 0x4000;
            const uint VK_SPACE = 0x20;
            const uint VK_H = 0x48;

            var rAltSpace = global::Windows.Win32.PInvoke.RegisterHotKey(
                (global::Windows.Win32.Foundation.HWND)helper.Handle,
                HotkeyIdAltSpace,
                (global::Windows.Win32.UI.Input.KeyboardAndMouse.HOT_KEY_MODIFIERS)(MOD_ALT | MOD_NOREPEAT),
                VK_SPACE);
            _logger.LogInformation("Native Hotkey Alt+Space registered: {Status}", rAltSpace.Value != 0);

            var rCtrlShiftSpace = global::Windows.Win32.PInvoke.RegisterHotKey(
                (global::Windows.Win32.Foundation.HWND)helper.Handle,
                HotkeyIdCtrlShiftSpace,
                (global::Windows.Win32.UI.Input.KeyboardAndMouse.HOT_KEY_MODIFIERS)(MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT),
                VK_SPACE);
            _logger.LogInformation("Native Hotkey Ctrl+Shift+Space registered: {Status}", rCtrlShiftSpace.Value != 0);

            var rCtrlAltSpace = global::Windows.Win32.PInvoke.RegisterHotKey(
                (global::Windows.Win32.Foundation.HWND)helper.Handle,
                HotkeyIdCtrlAltSpace,
                (global::Windows.Win32.UI.Input.KeyboardAndMouse.HOT_KEY_MODIFIERS)(MOD_CONTROL | MOD_ALT | MOD_NOREPEAT),
                VK_SPACE);
            _logger.LogInformation("Native Hotkey Ctrl+Alt+Space registered: {Status}", rCtrlAltSpace.Value != 0);

            var rWinShiftH = global::Windows.Win32.PInvoke.RegisterHotKey(
                (global::Windows.Win32.Foundation.HWND)helper.Handle,
                HotkeyIdWinShiftH,
                (global::Windows.Win32.UI.Input.KeyboardAndMouse.HOT_KEY_MODIFIERS)(MOD_WIN | MOD_SHIFT | MOD_NOREPEAT),
                VK_H);
            _logger.LogInformation("Native Hotkey Win+Shift+H registered: {Status}", rWinShiftH.Value != 0);

            var rCtrlShiftH = global::Windows.Win32.PInvoke.RegisterHotKey(
                (global::Windows.Win32.Foundation.HWND)helper.Handle,
                HotkeyIdCtrlShiftH,
                (global::Windows.Win32.UI.Input.KeyboardAndMouse.HOT_KEY_MODIFIERS)(MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT),
                VK_H);
            _logger.LogInformation("Native Hotkey Ctrl+Shift+H registered: {Status}", rCtrlShiftH.Value != 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register native RegisterHotKey fallback");
        }

        // Display prompt window immediately on launch
        var initialTarget = _windowCapture?.CaptureForegroundWindow();
        _popup.ShowForTarget(initialTarget);

        _logger.LogInformation("HolyHand application started. Dynamic Island active. Press Alt+Space, Win+Shift+H, or click the island to toggle.");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (id is HotkeyIdAltSpace or HotkeyIdCtrlShiftSpace or HotkeyIdCtrlAltSpace or HotkeyIdWinShiftH or HotkeyIdCtrlShiftH)
            {
                _logger?.LogInformation("WndProc received WM_HOTKEY with id 0x{Id:X}", id);
                OnHotkeyPressed(this, EventArgs.Empty);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            try
            {
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HolyHand");
                Directory.CreateDirectory(logDir);
                var logPath = Path.Combine(logDir, "holyhand.log");
                var serilogLogger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, shared: true)
                    .CreateLogger();
                builder.AddSerilog(serilogLogger);
            }
            catch { }
        });

        services.AddSingleton<ICredentialStore, CredentialStore>();
        services.AddSingleton<IWindowCaptureService, WindowCaptureService>();
        services.AddSingleton<IHotkeyService, LowLevelKeyboardHook>();
        services.AddSingleton<IAppLauncher, AppLauncher>();
        services.AddSingleton<IAudioService, WindowsAudioService>();
        services.AddSingleton<ISpeechInput>(sp => new WhisperSpeechService(
            fallbackService: null, // No fallback — WindowsSpeechService requires privacy policy acceptance
            logger: sp.GetService<ILogger<WhisperSpeechService>>()));
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        _logger?.LogInformation("App.OnHotkeyPressed received! Dispatching to UI thread...");
        Dispatcher.Invoke(() =>
        {
            try
            {
                if (_popup != null)
                {
                    if (_popup.IsExpanded)
                    {
                        _logger?.LogInformation("Dynamic Island already expanded; collapsing to top pill.");
                        _popup.Collapse();
                    }
                    else
                    {
                        var target = _windowCapture?.CaptureForegroundWindow();
                        _logger?.LogInformation("Trigger received. Expanding for target: {ProcessName} ({Title})",
                            target?.ProcessName ?? "None", target?.WindowTitle ?? "None");
                        _popup.ShowForTarget(target);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error toggling Dynamic Island in OnHotkeyPressed");
            }
        });
    }

    private void OnKillSwitchTriggered(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _logger?.LogInformation("Kill switch triggered. Cancelling active tasks.");
            _runCts?.Cancel();
            _popup?.HidePopup();
            _confirmationDialog?.Hide();
        });
    }

    private void OnTaskSubmitted(string goal, Core.Models.AppTarget? target)
    {
        _logger?.LogInformation("Goal submitted: '{Goal}' for app '{ProcessName}' (HWND: 0x{Hwnd:X})",
            goal, target?.ProcessName ?? "Unknown", target?.WindowHandle.ToInt64() ?? 0);

        if (target == null || target.WindowHandle == IntPtr.Zero)
        {
            target = WindowCaptureService.CaptureCurrentForegroundWindow()
                     ?? WindowCaptureService.CaptureWindowByProcessName("explorer")
                     ?? new Core.Models.AppTarget
                     {
                         ProcessId = Process.GetCurrentProcess().Id,
                         ProcessName = "explorer",
                         WindowTitle = "Desktop",
                         WindowHandle = IntPtr.Zero
                     };
            _logger?.LogInformation("Using fallback target: '{ProcessName}' (HWND: 0x{Hwnd:X})",
                target.ProcessName, target.WindowHandle.ToInt64());
        }

        _runCts?.Cancel();
        _runCts?.Dispose();
        _runCts = new CancellationTokenSource();
        var token = _runCts.Token;

        Task.Run(async () =>
        {
            try
            {
                var credentialStore = _serviceProvider?.GetService<ICredentialStore>() ?? new CredentialStore();
                var jevOptions = JevOptions.FromEnvironment();
                if (string.IsNullOrWhiteSpace(jevOptions.ApiKey))
                {
                    jevOptions.ApiKey = credentialStore.GetApiKey();
                }

                if (string.IsNullOrWhiteSpace(jevOptions.ApiKey))
                {
                    _logger?.LogWarning("No API key available for agent loop.");
                    _popup?.OnRunCompleted("API key is not configured. Please set AI_GATEWAY_API_KEY.", false);
                    return;
                }

                using var httpClient = new HttpClient();
                var jevClient = new JevClient(httpClient, jevOptions, NullLogger<JevClient>.Instance);
                var decisionModel = new JevDecisionModel(jevClient, jevOptions, NullLogger<JevDecisionModel>.Instance);

                var ocrService = new WindowsOcrService(NullLogger<WindowsOcrService>.Instance);
                using var screenReader = new UiaScreenReader(ScreenReaderOptions.Default, ocrService, NullLogger<UiaScreenReader>.Instance);
                var appLauncher = _serviceProvider?.GetService<IAppLauncher>() ?? new AppLauncher();
                var audioService = _serviceProvider?.GetService<IAudioService>() ?? new WindowsAudioService();
                using var actionExecutor = new ActionExecutor(NullLogger<ActionExecutor>.Instance, dryRun: false, appLauncher: appLauncher, audioService: audioService)
                {
                    TargetWindowHandle = target.WindowHandle,
                    ExpectedProcessId = target.ProcessId
                };

                if (target.WindowHandle != IntPtr.Zero)
                {
                    global::Windows.Win32.PInvoke.SetForegroundWindow((global::Windows.Win32.Foundation.HWND)target.WindowHandle);
                }

                using var auditLog = new JsonlAuditLog();
                var riskPolicy = new RiskPolicy();

                var loopOptions = new AgentLoopOptions
                {
                    DryRun = false,
                    MaxSteps = 0, // 0 = unlimited; runs until task completed or cancelled
                    MaxConsecutiveStalls = 3
                };

                var windowTracker = _serviceProvider?.GetService<IWindowCaptureService>() as IWindowTracker;

                var loop = new AgentLoop(
                    screenReader,
                    decisionModel,
                    actionExecutor,
                    loopOptions,
                    NullLogger<AgentLoop>.Instance,
                    riskPolicy,
                    _confirmationDialog,
                    auditLog,
                    windowTracker);

                loop.StatusChanged += status =>
                {
                    _popup?.UpdateStatus(status);
                };

                loop.TargetChanged += newTarget =>
                {
                    _popup?.UpdateTarget(newTarget);
                };

                var runResult = await loop.RunAsync(goal, target, token);
                _logger?.LogInformation("Agent loop finished with status {Status}: {Message}", runResult.Status, runResult.Message);

                bool isSuccess = runResult.Status == AgentRunStatus.Completed;
                _popup?.OnRunCompleted(runResult.Message ?? runResult.Status.ToString(), isSuccess);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Agent loop cancelled by user.");
                _popup?.OnRunCompleted("Cancelled by user", false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Agent loop encountered an unexpected error.");
                _popup?.OnRunCompleted(ex.Message, false);
            }
        }, token);
    }

    private void OnTaskCancelled()
    {
        _logger?.LogInformation("Goal input cancelled.");
        _runCts?.Cancel();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _runCts?.Cancel();
        _runCts?.Dispose();
        _hotkeyService?.Stop();
        _hotkeyService?.Dispose();

        if (_popup != null)
        {
            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(_popup);
                if (helper.Handle != IntPtr.Zero)
                {
                    global::Windows.Win32.PInvoke.UnregisterHotKey((global::Windows.Win32.Foundation.HWND)helper.Handle, HotkeyIdAltSpace);
                    global::Windows.Win32.PInvoke.UnregisterHotKey((global::Windows.Win32.Foundation.HWND)helper.Handle, HotkeyIdCtrlShiftSpace);
                    global::Windows.Win32.PInvoke.UnregisterHotKey((global::Windows.Win32.Foundation.HWND)helper.Handle, HotkeyIdCtrlAltSpace);
                    global::Windows.Win32.PInvoke.UnregisterHotKey((global::Windows.Win32.Foundation.HWND)helper.Handle, HotkeyIdWinShiftH);
                    global::Windows.Win32.PInvoke.UnregisterHotKey((global::Windows.Win32.Foundation.HWND)helper.Handle, HotkeyIdCtrlShiftH);
                }
            }
            catch { }
        }

        _activateEvent?.Dispose();
        _serviceProvider?.Dispose();

        if (_singleInstanceMutex != null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex wasn't owned
            }
            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}
