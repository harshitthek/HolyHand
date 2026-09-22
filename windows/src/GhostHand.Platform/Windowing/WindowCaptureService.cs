using System.Diagnostics;
using System.Drawing;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Models;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace HolyHand.Platform.Windowing;

public interface IWindowCaptureService
{
    AppTarget? CaptureForegroundWindow();
}

public class WindowCaptureService : IWindowCaptureService, IWindowTracker
{
    public AppTarget? CaptureForegroundWindow()
    {
        var hwnd = PInvoke.GetForegroundWindow();
        return CaptureWindowByHwnd(hwnd);
    }

    public AppTarget? GetActiveTarget(AppTarget currentTarget)
    {
        var fg = CaptureForegroundWindow();
        if (fg != null && fg.WindowHandle != IntPtr.Zero)
        {
            // If current target is the desktop or shell, auto-switch to active application
            if (IsDesktopOrShell(currentTarget) && !IsDesktopOrShell(fg))
            {
                return fg;
            }

            // If foreground window belongs to current target process, track its active window handle
            if (fg.ProcessId == currentTarget.ProcessId && fg.WindowHandle != currentTarget.WindowHandle)
            {
                return fg;
            }
        }
        return currentTarget;
    }

    public static bool IsDesktopOrShell(AppTarget? target)
    {
        if (target == null) return true;
        if (target.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(target.WindowTitle) ||
                target.WindowTitle.Equals("Program Manager", StringComparison.OrdinalIgnoreCase) ||
                target.WindowTitle.Equals("Desktop", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public static unsafe AppTarget? CaptureWindowByHwnd(HWND hwnd)
    {
        if (hwnd == HWND.Null)
        {
            return null;
        }

        uint processId = 0;
        unsafe
        {
            PInvoke.GetWindowThreadProcessId(hwnd, &processId);
        }

        if (processId == 0)
        {
            return null;
        }

        string processName = "Unknown";
        string exePath = string.Empty;
        bool isElevated = false;

        try
        {
            using var process = Process.GetProcessById((int)processId);
            processName = process.ProcessName;
            try
            {
                exePath = process.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                isElevated = true;
            }
        }
        catch
        {
            isElevated = true;
        }

        // Get window title
        string title = string.Empty;
        unsafe
        {
            fixed (char* titleBuffer = new char[512])
            {
                int len = PInvoke.GetWindowText(hwnd, titleBuffer, 512);
                if (len > 0)
                {
                    title = new string(titleBuffer, 0, len);
                }
            }
        }

        // Get window bounds
        var bounds = Rectangle.Empty;
        RECT winRect;
        if (PInvoke.GetWindowRect(hwnd, out winRect))
        {
            bounds = new Rectangle(winRect.left, winRect.top, winRect.right - winRect.left, winRect.bottom - winRect.top);
        }

        if (!isElevated)
        {
            isElevated = CheckElevationByProcessAccess((int)processId);
        }

        return new AppTarget
        {
            ProcessId = (int)processId,
            ProcessName = processName,
            ExecutablePath = exePath,
            WindowTitle = title,
            WindowHandle = (IntPtr)hwnd.Value,
            WindowBounds = bounds,
            IsElevated = isElevated
        };
    }

    public static unsafe AppTarget? CaptureWindowByProcessId(int processId)
    {
        HWND foundHwnd = HWND.Null;
        HWND fallbackHwnd = HWND.Null;

        // Check process MainWindowHandle first
        try
        {
            using var proc = Process.GetProcessById(processId);
            var mainHwnd = (HWND)proc.MainWindowHandle;
            if (mainHwnd != HWND.Null && PInvoke.IsWindowVisible(mainHwnd))
            {
                var mainTarget = CaptureWindowByHwnd(mainHwnd);
                if (mainTarget != null) return mainTarget;
            }
        }
        catch { }

        PInvoke.EnumWindows((hwnd, _) =>
        {
            uint pid = 0;
            PInvoke.GetWindowThreadProcessId(hwnd, &pid);
            if (pid == (uint)processId && PInvoke.IsWindowVisible(hwnd))
            {
                RECT rect;
                if (PInvoke.GetWindowRect(hwnd, out rect))
                {
                    int w = rect.right - rect.left;
                    int h = rect.bottom - rect.top;
                    // Normal window or minimized window (left == -32000)
                    if ((w > 150 && h > 150) || rect.left <= -30000)
                    {
                        foundHwnd = hwnd;
                        return false;
                    }
                }

                if (fallbackHwnd == HWND.Null)
                {
                    fallbackHwnd = hwnd;
                }
            }
            return true;
        }, 0);

        var finalHwnd = foundHwnd != HWND.Null ? foundHwnd : fallbackHwnd;
        return finalHwnd != HWND.Null ? CaptureWindowByHwnd(finalHwnd) : null;
    }

    public static AppTarget? CaptureWindowByProcessName(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        foreach (var proc in processes)
        {
            using (proc)
            {
                try
                {
                    var target = CaptureWindowByProcessId(proc.Id);
                    if (target != null) return target;
                }
                catch
                {
                    // Ignore inaccessible processes
                }
            }
        }
        return null;
    }

    public static bool IsTargetElevated(int processId) => CheckElevationByProcessAccess(processId);

    public static AppTarget? CaptureCurrentForegroundWindow() => new WindowCaptureService().CaptureForegroundWindow();

    public static bool CheckElevationByProcessAccess(int processId)
    {
        try
        {
            var hProcess = PInvoke.OpenProcess(
                Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
                false,
                (uint)processId);

            if (hProcess.IsNull)
            {
                // Access denied from standard user indicates elevated target
                return true;
            }

            PInvoke.CloseHandle(hProcess);
            return false;
        }
        catch
        {
            return true;
        }
    }
}
