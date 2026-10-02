using NAudio.CoreAudioApi;
using Microsoft.Extensions.Logging;
using HolyHand.Core.Interfaces;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace HolyHand.Platform.Audio;

public class WindowsAudioService : IAudioService
{
    private readonly ILogger<WindowsAudioService> _logger;

    public WindowsAudioService(ILogger<WindowsAudioService>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WindowsAudioService>.Instance;
    }

    public (float VolumePercent, bool IsMuted) GetMasterVolume()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var vol = device.AudioEndpointVolume.MasterVolumeLevelScalar * 100f;
            var muted = device.AudioEndpointVolume.Mute;
            return (vol, muted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read master volume via CoreAudio; defaulting to 50%");
            return (50f, false);
        }
    }

    public void SetMasterVolume(float volumePercent)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(volumePercent / 100f, 0f, 1f);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set master volume via CoreAudio");
        }
    }

    public void AdjustVolume(float deltaPercent)
    {
        var (current, _) = GetMasterVolume();
        SetMasterVolume(current + deltaPercent);
        ShowVolumeFlyout();
    }

    public bool ToggleMute()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.Mute = !device.AudioEndpointVolume.Mute;
            return device.AudioEndpointVolume.Mute;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to toggle mute via CoreAudio; falling back to VK_VOLUME_MUTE");
            Execution.InputSimulator.SendKey(VIRTUAL_KEY.VK_VOLUME_MUTE);
            return false;
        }
    }

    public void ShowVolumeFlyout()
    {
        try
        {
            Execution.InputSimulator.SendKey(VIRTUAL_KEY.VK_VOLUME_UP);
            Thread.Sleep(20);
            Execution.InputSimulator.SendKey(VIRTUAL_KEY.VK_VOLUME_DOWN);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show volume flyout via simulated keypresses");
        }
    }
}
