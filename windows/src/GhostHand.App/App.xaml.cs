using System.Net.Http;
using System.Windows;
using HolyHand.App.Windows;
using HolyHand.Core.Agent;
using HolyHand.Core.Common;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Jev;
using HolyHand.Core.Safety;
using HolyHand.Core.ScreenReading;
using HolyHand.Platform.Execution;
using HolyHand.Platform.Hotkey;
using HolyHand.Platform.Safety;
using HolyHand.Platform.ScreenReading;
using HolyHand.Platform.Windowing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HolyHand.App;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private const string MutexName = "Global\\HolyHand_SingleInstance_Mutex_2026";
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
            MessageBox.Show(
                "HolyHand is already running in the background.\nPress Ctrl+Win to activate.",
                "HolyHand",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        _logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        _windowCapture = _serviceProvider.GetRequiredService<IWindowCaptureService>();
        _hotkeyService = _serviceProvider.GetRequiredService<IHotkeyService>();

        // Pre-create windows (hidden at startup for instant display)
        _popup = new PromptPopupWindow();
        _popup.TaskSubmitted += OnTaskSubmitted;
        _popup.Cancelled += OnTaskCancelled;

        _confirmationDialog = new ConfirmationDialog();

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _hotkeyService.KillSwitchTriggered += OnKillSwitchTriggered;
        _hotkeyService.Start();

        _logger.LogInformation("HolyHand application started. Press Ctrl+Win to activate.");
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
        });

        services.AddSingleton<IWindowCaptureService, WindowCaptureService>();
        services.AddSingleton<IHotkeyService, LowLevelKeyboardHook>();
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var target = _windowCapture?.CaptureForegroundWindow();
            _logger?.LogInformation("Trigger received. Foreground target: {ProcessName} ({Title})", 
                target?.ProcessName ?? "None", target?.WindowTitle ?? "None");
            
            _popup?.ShowForTarget(target);
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
            _logger?.LogWarning("Cannot start agent run: No valid target window.");
            return;
        }

        _runCts?.Cancel();
        _runCts?.Dispose();
        _runCts = new CancellationTokenSource();
        var token = _runCts.Token;

        Task.Run(async () =>
        {
            try
            {
                var jevOptions = JevOptions.FromEnvironment();
                using var httpClient = new HttpClient();
                var jevClient = new JevClient(httpClient, jevOptions, NullLogger<JevClient>.Instance);
                var decisionModel = new JevDecisionModel(jevClient, jevOptions, NullLogger<JevDecisionModel>.Instance);

                var ocrService = new WindowsOcrService(NullLogger<WindowsOcrService>.Instance);
                using var screenReader = new UiaScreenReader(ScreenReaderOptions.Default, ocrService, NullLogger<UiaScreenReader>.Instance);
                using var actionExecutor = new ActionExecutor(NullLogger<ActionExecutor>.Instance, dryRun: false);
                using var auditLog = new JsonlAuditLog();
                var riskPolicy = new RiskPolicy();

                var loopOptions = new AgentLoopOptions
                {
                    DryRun = false,
                    MaxSteps = 15,
                    MaxConsecutiveStalls = 3
                };

                var loop = new AgentLoop(
                    screenReader,
                    decisionModel,
                    actionExecutor,
                    loopOptions,
                    NullLogger<AgentLoop>.Instance,
                    riskPolicy,
                    _confirmationDialog,
                    auditLog);

                loop.StatusChanged += status =>
                {
                    _popup?.UpdateStatus(status);
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
