using System.Windows;
using HolyHand.App.Windows;
using HolyHand.Core.Common;
using HolyHand.Core.Interfaces;
using HolyHand.Platform.Hotkey;
using HolyHand.Platform.Windowing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HolyHand.App;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private const string MutexName = "Global\\HolyHand_SingleInstance_Mutex_2026";
    private ServiceProvider? _serviceProvider;

    private IHotkeyService? _hotkeyService;
    private IWindowCaptureService? _windowCapture;
    private PromptPopupWindow? _popup;
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

        // Pre-create popup window (hidden at startup for instant display)
        _popup = new PromptPopupWindow();
        _popup.TaskSubmitted += OnTaskSubmitted;
        _popup.Cancelled += OnTaskCancelled;

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
            _logger?.LogInformation("Kill switch triggered. Hiding popup.");
            _popup?.HidePopup();
        });
    }

    private void OnTaskSubmitted(string goal, Core.Models.AppTarget? target)
    {
        _logger?.LogInformation("Goal submitted: '{Goal}' for app '{ProcessName}' (HWND: 0x{Hwnd:X})",
            goal, target?.ProcessName ?? "Unknown", target?.WindowHandle.ToInt64() ?? 0);
    }

    private void OnTaskCancelled()
    {
        _logger?.LogInformation("Goal input cancelled.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
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
