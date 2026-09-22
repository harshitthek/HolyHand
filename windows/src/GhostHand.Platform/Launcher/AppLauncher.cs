using System.Diagnostics;
using HolyHand.Core.Agent;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Jev;
using HolyHand.Core.Models;
using HolyHand.Platform.Windowing;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace HolyHand.Platform.Launcher;

public class AppLauncher : IAppLauncher
{
    private readonly ILogger<AppLauncher> _logger;

    private static readonly Dictionary<string, string> KnownApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["settings"] = "ms-settings:",
        ["windows settings"] = "ms-settings:",
        ["notepad"] = "notepad.exe",
        ["text editor"] = "notepad.exe",
        ["calculator"] = "calc.exe",
        ["calc"] = "calc.exe",
        ["explorer"] = "explorer.exe",
        ["file explorer"] = "explorer.exe",
        ["files"] = "explorer.exe",
        ["edge"] = "msedge.exe",
        ["microsoft edge"] = "msedge.exe",
        ["browser"] = "msedge.exe",
        ["chrome"] = "chrome.exe",
        ["google chrome"] = "chrome.exe",
        ["terminal"] = "wt.exe",
        ["windows terminal"] = "wt.exe",
        ["cmd"] = "cmd.exe",
        ["command prompt"] = "cmd.exe",
        ["paint"] = "mspaint.exe",
        ["task manager"] = "taskmgr.exe",
        ["spotify"] = "spotify.exe"
    };

    private static readonly HashSet<string> DisallowedExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "powershell.exe",
        "pwsh.exe",
        "wscript.exe",
        "cscript.exe",
        "mshta.exe",
        "reg.exe",
        "regedit.exe",
        "format.com",
        "diskpart.exe",
        "rundll32.exe"
    };

    public AppLauncher(ILogger<AppLauncher>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AppLauncher>.Instance;
    }

    public bool TryExtractAppLaunch(string goal, out string appName, out string launchCommand)
    {
        appName = string.Empty;
        launchCommand = string.Empty;

        var candidates = JevDecisionModel.ExtractAppLaunchCandidates(goal);
        if (candidates.Count == 0) return false;

        foreach (var candidate in candidates)
        {
            if (ResolveLaunchCommand(candidate, out var resolvedCommand))
            {
                appName = candidate;
                launchCommand = resolvedCommand;
                return true;
            }
        }

        // Default to the first candidate as executable if it is safe
        var first = candidates[0];
        if (IsSafeLaunchCommand(first))
        {
            appName = first;
            launchCommand = first.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? first : first + ".exe";
            return true;
        }

        return false;
    }

    public bool TryExtractUrlLaunch(string goal, out Uri url)
    {
        url = null!;
        var urls = UrlLauncherValidator.ExtractWebUrls(goal);
        if (urls.Count > 0)
        {
            url = urls[0];
            return true;
        }
        return false;
    }

    public async Task<AppTarget?> LaunchAppAsync(string appName, string? launchCommand = null, CancellationToken cancellationToken = default)
    {
        var command = launchCommand;
        if (string.IsNullOrEmpty(command))
        {
            if (!ResolveLaunchCommand(appName, out command))
            {
                command = appName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? appName : appName + ".exe";
            }
        }

        // Check if application is already running with an open window -> focus it!
        var runningTarget = FindRunningAppTarget(appName, command);
        if (runningTarget != null && runningTarget.WindowHandle != IntPtr.Zero)
        {
            _logger.LogInformation("Application '{AppName}' is already running ({ProcessName}). Bringing window to foreground.",
                appName, runningTarget.ProcessName);
            BringWindowToForeground(runningTarget);
            return runningTarget;
        }

        if (!IsSafeLaunchCommand(command))
        {
            _logger.LogWarning("Refusing to launch command '{Command}': safety violation.", command);
            throw new InvalidOperationException($"Launch of '{command}' was blocked by safety policy.");
        }

        _logger.LogInformation("Launching application '{AppName}' via command '{Command}'", appName, command);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                UseShellExecute = true
            };

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start process for command '{Command}'", command);
            return null;
        }

        // Wait for window to appear and become foreground
        var expectedProcess = GetExpectedProcessName(appName, command);
        return await WaitForNewForegroundTargetAsync(expectedProcess, cancellationToken);
    }

    public async Task<AppTarget?> LaunchUrlAsync(Uri url, CancellationToken cancellationToken = default)
    {
        if (!UrlLauncherValidator.IsValidWebUrl(url.ToString(), out var validatedUri) || validatedUri == null)
        {
            throw new ArgumentException($"Invalid or non-http/https URL '{url}'", nameof(url));
        }

        _logger.LogInformation("Launching URL: {Url}", validatedUri);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = validatedUri.ToString(),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open URL '{Url}'", validatedUri);
            return null;
        }

        return await WaitForNewForegroundTargetAsync(null, cancellationToken);
    }

    private static bool ResolveLaunchCommand(string appName, out string launchCommand)
    {
        var trimmed = appName.Trim();
        var clean = trimmed.ToLowerInvariant();

        // 1. Known predefined apps
        if (KnownApps.TryGetValue(trimmed, out var command) || KnownApps.TryGetValue(clean, out command))
        {
            launchCommand = command;
            return true;
        }

        // 2. Windows App Paths registry lookup (registered desktop apps)
        if (TryFindInAppPathsRegistry(trimmed, out var appPath))
        {
            launchCommand = appPath;
            return true;
        }

        // 3. Windows Start Menu shortcut (.lnk)
        if (TryFindStartMenuShortcut(trimmed, out var shortcutPath))
        {
            launchCommand = shortcutPath;
            return true;
        }

        // 4. Known protocol schemes
        if (clean is "spotify" or "discord" or "slack" or "steam")
        {
            launchCommand = $"{clean}:";
            return true;
        }

        launchCommand = string.Empty;
        return false;
    }

    private static bool TryFindInAppPathsRegistry(string appName, out string path)
    {
        path = string.Empty;
        var exeName = appName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? appName : appName + ".exe";

        string[] baseKeys = [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths"
        ];

        foreach (var baseKey in baseKeys)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"{baseKey}\{exeName}");
                var val = key?.GetValue(null)?.ToString();
                if (!string.IsNullOrEmpty(val))
                {
                    val = val.Trim('"', ' ');
                    if (File.Exists(val))
                    {
                        path = val;
                        return true;
                    }
                }
            }
            catch { }

            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey($@"{baseKey}\{exeName}");
                var val = key?.GetValue(null)?.ToString();
                if (!string.IsNullOrEmpty(val))
                {
                    val = val.Trim('"', ' ');
                    if (File.Exists(val))
                    {
                        path = val;
                        return true;
                    }
                }
            }
            catch { }
        }

        return false;
    }

    private static bool TryFindStartMenuShortcut(string appName, out string shortcutPath)
    {
        shortcutPath = string.Empty;
        var searchToken = appName.Trim().ToLowerInvariant();

        var directories = new List<string>();
        var commonStart = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
        if (!string.IsNullOrEmpty(commonStart) && Directory.Exists(commonStart))
            directories.Add(commonStart);

        var userStart = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        if (!string.IsNullOrEmpty(userStart) && Directory.Exists(userStart))
            directories.Add(userStart);

        foreach (var dir in directories)
        {
            try
            {
                var lnkFiles = Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories);
                // Exact match first
                foreach (var file in lnkFiles)
                {
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
                    if (nameWithoutExt.Equals(searchToken, StringComparison.OrdinalIgnoreCase))
                    {
                        shortcutPath = file;
                        return true;
                    }
                }

                // Substring match
                foreach (var file in lnkFiles)
                {
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
                    if (nameWithoutExt.Contains(searchToken, StringComparison.OrdinalIgnoreCase))
                    {
                        shortcutPath = file;
                        return true;
                    }
                }
            }
            catch { }
        }

        return false;
    }

    private static AppTarget? FindRunningAppTarget(string appName, string? command)
    {
        var expectedName = !string.IsNullOrEmpty(command) ? GetExpectedProcessName(appName, command) : appName;
        if (string.IsNullOrEmpty(expectedName)) expectedName = appName;

        // 1. Direct match by process name
        var target = WindowCaptureService.CaptureWindowByProcessName(expectedName);
        if (target != null && target.WindowHandle != IntPtr.Zero)
            return target;

        // 2. Search processes by process name or main window title
        try
        {
            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                using (proc)
                {
                    try
                    {
                        if (proc.ProcessName.Contains(expectedName, StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrEmpty(proc.MainWindowTitle) && proc.MainWindowTitle.Contains(expectedName, StringComparison.OrdinalIgnoreCase)))
                        {
                            var captured = WindowCaptureService.CaptureWindowByProcessId(proc.Id);
                            if (captured != null && captured.WindowHandle != IntPtr.Zero)
                                return captured;
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        return null;
    }

    private static void BringWindowToForeground(AppTarget target)
    {
        if (target.WindowHandle == IntPtr.Zero) return;
        try
        {
            var hwnd = (HWND)target.WindowHandle;
            PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_RESTORE);
            PInvoke.SetForegroundWindow(hwnd);
        }
        catch { }
    }

    private static bool IsSafeLaunchCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;

        var exeName = Path.GetFileName(command).Trim();
        if (DisallowedExecutables.Contains(exeName)) return false;

        // Block shell command line injection
        if (command.Contains(" -enc ") || command.Contains(" /c ") || command.Contains(" /k ") ||
            command.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
            command.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) ||
            command.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
            command.EndsWith(".vbs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string? GetExpectedProcessName(string appName, string command)
    {
        if (command.Equals("ms-settings:", StringComparison.OrdinalIgnoreCase) ||
            appName.Contains("settings", StringComparison.OrdinalIgnoreCase))
        {
            return "SystemSettings";
        }

        if (command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileNameWithoutExtension(command);
        }

        return null;
    }

    private async Task<AppTarget?> WaitForNewForegroundTargetAsync(string? expectedProcessName, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        AppTarget? lastCaptured = null;

        while (sw.ElapsedMilliseconds < 3500)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(250, cancellationToken);

            var fg = WindowCaptureService.CaptureCurrentForegroundWindow();
            if (fg != null && fg.WindowHandle != IntPtr.Zero)
            {
                // Skip taskbar and shell desktop
                if (fg.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
                    (fg.WindowTitle.Equals("Program Manager", StringComparison.OrdinalIgnoreCase) ||
                     string.IsNullOrEmpty(fg.WindowTitle)))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(expectedProcessName))
                {
                    if (fg.ProcessName.Contains(expectedProcessName, StringComparison.OrdinalIgnoreCase) ||
                        fg.WindowTitle.Contains(expectedProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Captured newly launched target window: {ProcessName} - '{Title}'", fg.ProcessName, fg.WindowTitle);
                        return fg;
                    }
                }
                else
                {
                    lastCaptured = fg;
                }
            }
        }

        if (!string.IsNullOrEmpty(expectedProcessName))
        {
            var byProc = WindowCaptureService.CaptureWindowByProcessName(expectedProcessName);
            if (byProc != null)
            {
                _logger.LogInformation("Captured launched window by process name: {ProcessName} - '{Title}'", byProc.ProcessName, byProc.WindowTitle);
                return byProc;
            }
        }

        return lastCaptured ?? WindowCaptureService.CaptureCurrentForegroundWindow();
    }
}
