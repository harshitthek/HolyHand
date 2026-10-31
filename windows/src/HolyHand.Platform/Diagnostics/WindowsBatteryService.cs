using System.Runtime.InteropServices;
using HolyHand.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace HolyHand.Platform.Diagnostics;

public class WindowsBatteryService : IBatteryService
{
    private readonly ILogger<WindowsBatteryService> _logger;

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    public WindowsBatteryService(ILogger<WindowsBatteryService>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WindowsBatteryService>.Instance;
    }

    public BatteryInfo GetBatteryInfo()
    {
        try
        {
            if (!GetSystemPowerStatus(out var status))
            {
                _logger.LogWarning("GetSystemPowerStatus returned false.");
                return new BatteryInfo(
                    BatteryLifePercent: -1,
                    IsCharging: false,
                    IsAcPlugged: false,
                    EstimatedTimeRemaining: null,
                    Summary: "Battery status unavailable.");
            }

            int percent = status.BatteryLifePercent != 255 ? status.BatteryLifePercent : -1;
            bool isAc = status.ACLineStatus == 1;
            bool isCharging = (status.BatteryFlag & 8) != 0;
            bool noBattery = (status.BatteryFlag & 128) != 0;

            if (noBattery)
            {
                return new BatteryInfo(
                    BatteryLifePercent: 100,
                    IsCharging: false,
                    IsAcPlugged: true,
                    EstimatedTimeRemaining: null,
                    Summary: "Desktop / AC power (no battery detected).");
            }

            TimeSpan? timeLeft = null;
            if (status.BatteryLifeTime > 0 && status.BatteryLifeTime != -1)
            {
                timeLeft = TimeSpan.FromSeconds(status.BatteryLifeTime);
            }

            string state = isCharging ? "Charging" : (isAc ? "Plugged in (not charging)" : "On battery");
            string timeStr = timeLeft.HasValue ? $" (~{timeLeft.Value.Hours}h {timeLeft.Value.Minutes}m remaining)" : "";
            string summary = percent >= 0
                ? $"Battery is at {percent}% - {state}{timeStr}"
                : $"Power state: {state}{timeStr}";

            return new BatteryInfo(
                BatteryLifePercent: percent,
                IsCharging: isCharging,
                IsAcPlugged: isAc,
                EstimatedTimeRemaining: timeLeft,
                Summary: summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading system power status");
            return new BatteryInfo(
                BatteryLifePercent: -1,
                IsCharging: false,
                IsAcPlugged: false,
                EstimatedTimeRemaining: null,
                Summary: "Error reading battery status.");
        }
    }
}
