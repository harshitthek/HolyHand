using System.IO;
using System.Net.Http;
using System.Text;
using HolyHand.Core.Interfaces;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Whisper.net;
using Whisper.net.Ggml;

namespace HolyHand.Platform.Speech;

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
        // 1. Check if audio capture device is available
        if (WaveInEvent.DeviceCount == 0)
        {
            _logger.LogWarning("No audio recording devices found.");
            if (_fallbackService != null)
            {
                _logger.LogInformation("Falling back to secondary speech service...");
                return await _fallbackService.TranscribeAsync(cancellationToken);
            }
            throw new InvalidOperationException("No microphone or audio input device found on this system.");
        }

        // 2. Ensure model is downloaded and Whisper initialized
        try
        {
            await EnsureInitializedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Whisper.net initialization failed. Attempting fallback...");
            if (_fallbackService != null)
            {
                return await _fallbackService.TranscribeAsync(cancellationToken);
            }
            throw new InvalidOperationException("Whisper speech model is unavailable and no fallback recognizer is present: " + ex.Message, ex);
        }

        // 3. Record audio from microphone
        using var audioStream = new MemoryStream();
        var waveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit mono PCM required by Whisper
        var recordingStoppedTcs = new TaskCompletionSource<bool>();

        WaveInEvent? waveIn = null;
        try
        {
            waveIn = new WaveInEvent
            {
                WaveFormat = waveFormat,
                BufferMilliseconds = 100
            };

            waveIn.DataAvailable += (s, e) =>
            {
                if (e.BytesRecorded > 0)
                {
                    audioStream.Write(e.Buffer, 0, e.BytesRecorded);
                }
            };

            waveIn.RecordingStopped += (s, e) =>
            {
                if (e.Exception != null)
                {
                    recordingStoppedTcs.TrySetException(e.Exception);
                }
                else
                {
                    recordingStoppedTcs.TrySetResult(true);
                }
            };

            _logger.LogInformation("Starting microphone audio capture...");
            waveIn.StartRecording();

            // Wait until cancellation (user released button / stopped) or timeout (max 30s)
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                await Task.Delay(Timeout.Infinite, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when user finishes speaking
            }

            waveIn.StopRecording();
            await recordingStoppedTcs.Task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Microphone capture error. Checking permissions or device access.");
            if (_fallbackService != null)
            {
                return await _fallbackService.TranscribeAsync(cancellationToken);
            }
            throw new InvalidOperationException("Microphone recording failed. Please check microphone permissions: " + ex.Message, ex);
        }
        finally
        {
            waveIn?.Dispose();
        }

        // If no audio recorded
        if (audioStream.Length == 0)
        {
            _logger.LogInformation("No audio data captured.");
            return string.Empty;
        }

        // 4. Wrap PCM in valid WAV format for Whisper processor
        audioStream.Position = 0;
        using var wavStream = new MemoryStream();
        using (var writer = new WaveFileWriter(wavStream, waveFormat))
        {
            var pcmBytes = audioStream.ToArray();
            writer.Write(pcmBytes, 0, pcmBytes.Length);
            writer.Flush();
        }

        wavStream.Position = 0;

        // 5. Run Whisper local CPU inference
        try
        {
            _logger.LogInformation("Running local Whisper transcription on captured audio ({Length} bytes)...", wavStream.Length);
            var sb = new StringBuilder();

            if (_processor == null)
            {
                throw new InvalidOperationException("Whisper processor is not initialized.");
            }

            await foreach (var segment in _processor.ProcessAsync(wavStream, cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(segment.Text))
                {
                    sb.Append(segment.Text).Append(' ');
                    SpeechRecognizing?.Invoke(segment.Text);
                }
            }

            var result = sb.ToString().Trim();
            _logger.LogInformation("Whisper transcription result: \"{Result}\"", result);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Transcription cancelled.");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Whisper transcription failed.");
            if (_fallbackService != null)
            {
                _logger.LogInformation("Falling back to secondary speech service after Whisper failure...");
                return await _fallbackService.TranscribeAsync(cancellationToken);
            }
            throw new InvalidOperationException("Whisper transcription failed: " + ex.Message, ex);
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
                _logger.LogInformation("Whisper model not found at {Path}. Downloading ggml-tiny model on demand...", _modelPath);
                await DownloadModelAsync(_modelPath, cancellationToken);
            }

            _logger.LogInformation("Loading Whisper model from {Path}...", _modelPath);
            _factory = WhisperFactory.FromPath(_modelPath);
            _processor = _factory.CreateBuilder()
                .WithLanguage("auto")
                .Build();

            _logger.LogInformation("Whisper processor successfully initialized.");
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task DownloadModelAsync(string destinationPath, CancellationToken cancellationToken)
    {
        var tempPath = destinationPath + ".downloading";
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            
            // Try WhisperGgmlDownloader first, or direct HuggingFace download
            Stream modelStream;
            try
            {
                modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Tiny, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WhisperGgmlDownloader failed, trying direct HuggingFace download...");
                var downloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin";
                modelStream = await httpClient.GetStreamAsync(downloadUrl, cancellationToken);
            }

            using (modelStream)
            using (var fileStream = File.Create(tempPath))
            {
                await modelStream.CopyToAsync(fileStream, cancellationToken);
            }

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
            File.Move(tempPath, destinationPath);
            _logger.LogInformation("Whisper model downloaded successfully to {Path}", destinationPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download Whisper model.");
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* Ignore */ }
            }
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
