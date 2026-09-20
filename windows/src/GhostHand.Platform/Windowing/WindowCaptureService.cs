using System.Diagnostics;
using System.Drawing;
using HolyHand.Core.Models;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace HolyHand.Platform.Windowing;

public interface IWindowCaptureService
{
    AppTarget? CaptureForegroundWindow();
}

public class WindowCaptureService : IWindowCaptureService
{
    public unsafe AppTarget? CaptureForegroundWindow()
    {
        var hwnd = PInvoke.GetForegroundWindow();
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
                // Access denied on elevated/system process
                isElevated = true;
            }
        }
        catch
        {
            // Process may have exited or is elevated
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

    private static bool CheckElevationByProcessAccess(int processId)
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
