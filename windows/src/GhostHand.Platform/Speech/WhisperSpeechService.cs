using System.IO;
using System.Net.Http;
using System.Text;
using HolyHand.Core.Interfaces;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Whisper.net;
using Whisper.net.Ggml;

namespace HolyHand.Platform.Speech;

/// <summary>
/// Local Whisper.net speech-to-text service using NAudio microphone capture.
/// Records until silence is detected (auto-stop) or cancellation is requested.
/// No Windows privacy policy required - runs entirely offline using the local Whisper model.
/// </summary>
public class WhisperSpeechService : ISpeechInput
{
    private readonly ILogger<WhisperSpeechService> _logger;
    private readonly ISpeechInput? _fallbackService;
    private readonly string _modelsDir;
    private readonly string _modelPath;
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _disposed;

    // Auto-silence detection: stop recording after this many ms of silence
    private const int SilenceThresholdMs = 1800;
    // Silence RMS threshold: audio samples below this are considered silence
    private const float SilenceRmsThreshold = 0.01f;
    // Minimum recording time before silence detection kicks in
    private const int MinRecordingMs = 500;

    public event Action<string>? SpeechRecognizing;

    public WhisperSpeechService(
        ISpeechInput? fallbackService = null,
        string? customModelDir = null,
        ILogger<WhisperSpeechService>? logger = null)
    {
        _fallbackService = fallbackService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WhisperSpeechService>.Instance;

        _modelsDir = customModelDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HolyHand",
            "models");
        _modelPath = Path.Combine(_modelsDir, "ggml-tiny.bin");
    }

    public async Task<string> TranscribeAsync(CancellationToken cancellationToken = default)
    {
        // 1. Check if any microphone / audio recording device is available
        bool hasDevice = false;
        try
        {
            if (WaveInEvent.DeviceCount > 0)
            {
                hasDevice = true;
            }
            else
            {
                using var enumerator = new MMDeviceEnumerator();
                var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                hasDevice = endpoints.Count > 0;
            }
        }
        catch
        {
            hasDevice = WaveInEvent.DeviceCount > 0;
        }

        if (!hasDevice)
        {
            _logger.LogWarning("No microphone/audio recording device found.");
            if (_fallbackService != null)
            {
                return await _fallbackService.TranscribeAsync(cancellationToken);
            }
            throw new InvalidOperationException(
                "No active microphone detected. Connect your Bluetooth headphones (e.g. OnePlus Bullets), plug in a headset/mic, or check Windows Settings → Privacy & Security → Microphone.");
        }

        // 2. Initialize Whisper model (downloads ggml-tiny on first run if missing)
        try
        {
            await EnsureInitializedAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Whisper model initialization failed.");
            if (_fallbackService != null)
            {
                _logger.LogInformation("Attempting fallback speech service...");
                return await _fallbackService.TranscribeAsync(cancellationToken);
            }
            throw new InvalidOperationException(
                "Could not load speech model. Check your internet connection for first-time model download: " + ex.Message, ex);
        }

        // 3. Record microphone audio with silence-based auto-stop
        using var audioStream = new MemoryStream();
        var targetWaveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit mono - required by Whisper

        var recordingStoppedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var silenceSince = DateTime.UtcNow;
        var recordingStarted = DateTime.UtcNow;
        bool hasSpeech = false;

        IWaveIn? waveIn = null;
        try
        {
            if (WaveInEvent.DeviceCount > 0)
            {
                waveIn = new WaveInEvent
                {
                    WaveFormat = targetWaveFormat,
                    BufferMilliseconds = 100 // 100ms chunks
                };
            }
            else
            {
                waveIn = new WasapiCapture();
            }

            var captureFormat = waveIn.WaveFormat;

            waveIn.DataAvailable += (s, e) =>
            {
                if (e.BytesRecorded <= 0) return;

                audioStream.Write(e.Buffer, 0, e.BytesRecorded);

                // Compute audio energy (RMS) to detect speech vs silence
                float rms = ComputeRms(e.Buffer, e.BytesRecorded, captureFormat);
                if (rms >= SilenceRmsThreshold)
                {
                    hasSpeech = true;
                    silenceSince = DateTime.UtcNow;
                }

                // Auto-stop after silence detected (only after minimum recording time)
                var elapsed = (DateTime.UtcNow - recordingStarted).TotalMilliseconds;
                var silenceDuration = (DateTime.UtcNow - silenceSince).TotalMilliseconds;

                if (hasSpeech && elapsed >= MinRecordingMs && silenceDuration >= SilenceThresholdMs)
                {
                    try { waveIn?.StopRecording(); } catch { /* ignore */ }
                }
            };

            waveIn.RecordingStopped += (s, e) =>
            {
                if (e.Exception != null)
                    recordingStoppedTcs.TrySetException(e.Exception);
                else
                    recordingStoppedTcs.TrySetResult(true);
            };

            _logger.LogInformation("Starting microphone recording (auto-stops after silence)...");
            recordingStarted = DateTime.UtcNow;
            silenceSince = DateTime.UtcNow;
            waveIn.StartRecording();

            // Wait: either user clicks Stop, silence auto-stops, or 30s timeout
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                await Task.WhenAny(
                    recordingStoppedTcs.Task,
                    Task.Delay(Timeout.Infinite, linkedCts.Token));
            }
            catch (OperationCanceledException)
            {
                // User cancelled or timeout
            }

            try { waveIn.StopRecording(); } catch { /* ignore */ }
            await recordingStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Microphone recording error.");
            if (_fallbackService != null)
            {
                return await _fallbackService.TranscribeAsync(cancellationToken);
            }
            throw new InvalidOperationException(
                "Microphone recording failed. Check microphone permissions in Windows Settings → Privacy & Security → Microphone. " + ex.Message, ex);
        }
        finally
        {
            waveIn?.Dispose();
        }

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Voice recording cancelled by user.");
            return string.Empty;
        }

        // 4. Check if we got any audio
        if (audioStream.Length == 0 || !hasSpeech)
        {
            _logger.LogInformation("No speech detected in recording.");
            return string.Empty;
        }

        // 5. Convert audio to 16kHz 16-bit mono WAV for Whisper
        audioStream.Position = 0;
        using var wavStream = new MemoryStream();
        try
        {
            var rawBytes = audioStream.ToArray();
            var captureFormat = waveIn?.WaveFormat ?? targetWaveFormat;

            if (captureFormat.SampleRate == 16000 && captureFormat.BitsPerSample == 16 && captureFormat.Channels == 1)
            {
                using var writer = new WaveFileWriter(wavStream, targetWaveFormat);
                writer.Write(rawBytes, 0, rawBytes.Length);
                writer.Flush();
            }
            else
            {
                // Resample to 16kHz 16-bit mono using NAudio resampler
                using var rawStream = new MemoryStream(rawBytes);
                using var rawReader = new RawSourceWaveStream(rawStream, captureFormat);
                using var resampler = new MediaFoundationResampler(rawReader, targetWaveFormat)
                {
                    ResamplerQuality = 60
                };
                WaveFileWriter.WriteWavFileToStream(wavStream, resampler);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audio resampling failed, falling back to direct PCM header write.");
            wavStream.SetLength(0);
            using var writer = new WaveFileWriter(wavStream, targetWaveFormat);
            var pcmBytes = audioStream.ToArray();
            writer.Write(pcmBytes, 0, pcmBytes.Length);
            writer.Flush();
        }

        wavStream.Position = 0;

        // 6. Transcribe with local Whisper
        try
        {
            _logger.LogInformation("Transcribing {Length} bytes of audio with Whisper...", wavStream.Length);
            var sb = new StringBuilder();

            if (_processor == null)
                throw new InvalidOperationException("Whisper processor is not ready.");

            await foreach (var segment in _processor.ProcessAsync(wavStream, cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(segment.Text))
                {
                    var text = segment.Text.Trim();
                    sb.Append(text).Append(' ');
                    SpeechRecognizing?.Invoke(text);
                }
            }

            var result = sb.ToString().Trim();
            _logger.LogInformation("Whisper transcription result: \"{Result}\"", result);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Whisper transcription cancelled.");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Whisper transcription failed.");
            if (_fallbackService != null)
            {
                return await _fallbackService.TranscribeAsync(cancellationToken);
            }
            throw new InvalidOperationException("Speech transcription failed: " + ex.Message, ex);
        }
    }

    /// <summary>Compute RMS (volume) of audio buffer.</summary>
    private static float ComputeRms(byte[] buffer, int bytesRecorded, WaveFormat format)
    {
        if (bytesRecorded < 2) return 0f;

        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            int floatCount = bytesRecorded / 4;
            double sum = 0;
            for (int i = 0; i < bytesRecorded - 3; i += 4)
            {
                float sample = BitConverter.ToSingle(buffer, i);
                sum += sample * sample;
            }
            return (float)Math.Sqrt(sum / Math.Max(1, floatCount));
        }
        else
        {
            int sampleCount = bytesRecorded / 2;
            double sum = 0;
            for (int i = 0; i < bytesRecorded - 1; i += 2)
            {
                short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                double normalized = sample / 32768.0;
                sum += normalized * normalized;
            }
            return (float)Math.Sqrt(sum / Math.Max(1, sampleCount));
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_processor != null) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_processor != null) return;

            Directory.CreateDirectory(_modelsDir);

            if (!File.Exists(_modelPath))
            {
                _logger.LogInformation("Whisper model not found. Downloading ggml-tiny (~75MB) on first run...");
                await DownloadModelAsync(_modelPath, cancellationToken);
            }

            _logger.LogInformation("Loading Whisper model from {Path}...", _modelPath);
            _factory = WhisperFactory.FromPath(_modelPath);
            _processor = _factory.CreateBuilder()
                .WithLanguage("auto")
                .WithNoContext()
                .WithSingleSegment()
                .Build();

            _logger.LogInformation("Whisper processor initialized successfully.");
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task DownloadModelAsync(string destinationPath, CancellationToken cancellationToken)
    {
        var tempPath = destinationPath + ".downloading";
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        try
        {
            _logger.LogInformation("Downloading Whisper tiny model...");
            Stream modelStream;
            try
            {
                modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Tiny, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WhisperGgmlDownloader failed, trying HuggingFace direct download...");
                modelStream = await httpClient.GetStreamAsync(
                    "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin",
                    cancellationToken);
            }

            using (modelStream)
            using (var fileStream = File.Create(tempPath))
            {
                await modelStream.CopyToAsync(fileStream, cancellationToken);
            }

            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            File.Move(tempPath, destinationPath);
            _logger.LogInformation("Whisper model downloaded to {Path}", destinationPath);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* ignore */ }
            throw;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _processor?.Dispose();
            _factory?.Dispose();
            _initLock.Dispose();
            _fallbackService?.Dispose();
        }
    }
}
