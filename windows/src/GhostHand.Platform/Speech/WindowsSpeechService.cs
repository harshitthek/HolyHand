using HolyHand.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Windows.Media.SpeechRecognition;

namespace HolyHand.Platform.Speech;

public class WindowsSpeechService : ISpeechInput
{
    private readonly ILogger<WindowsSpeechService> _logger;
    private SpeechRecognizer? _recognizer;
    private bool _initialized;
    private bool _disposed;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public event Action<string>? SpeechRecognizing;

    public WindowsSpeechService(ILogger<WindowsSpeechService>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WindowsSpeechService>.Instance;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized && _recognizer != null) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized && _recognizer != null) return;

            _logger.LogInformation("Initializing Windows SpeechRecognizer...");
            _recognizer = new SpeechRecognizer();

            // Enable dictation topic constraint for open-ended vocabulary
            var dictationConstraint = new SpeechRecognitionTopicConstraint(
                SpeechRecognitionScenario.Dictation, "dictation");
            _recognizer.Constraints.Add(dictationConstraint);

            var compilationResult = await _recognizer.CompileConstraintsAsync();
            if (compilationResult.Status != SpeechRecognitionResultStatus.Success)
            {
                _logger.LogWarning("SpeechRecognizer compilation status: {Status}", compilationResult.Status);
            }

            _recognizer.HypothesisGenerated += (sender, args) =>
            {
                var text = args.Hypothesis?.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    SpeechRecognizing?.Invoke(text);
                }
            };

            _initialized = true;
            _logger.LogInformation("Windows SpeechRecognizer initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Windows SpeechRecognizer. Microphone or speech language may be unavailable.");
            _recognizer?.Dispose();
            _recognizer = null;
            throw new InvalidOperationException("Could not initialize microphone or Windows speech recognition. Please ensure microphone access is enabled.", ex);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<string> TranscribeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        if (_recognizer == null)
        {
            throw new InvalidOperationException("Speech recognizer is not available on this machine.");
        }

        _logger.LogInformation("Starting voice capture and transcription...");

        try
        {
            // Execute single-shot recognition with cancellation support
            var recognitionTask = _recognizer.RecognizeAsync().AsTask(cancellationToken);
            var result = await recognitionTask;

            if (result.Status == SpeechRecognitionResultStatus.Success && !string.IsNullOrWhiteSpace(result.Text))
            {
                _logger.LogInformation("Speech recognition completed: \"{Text}\" (Confidence: {Confidence})",
                    result.Text, result.Confidence);
                return result.Text.Trim();
            }

            _logger.LogWarning("Speech recognition ended with status {Status}", result.Status);
            return string.Empty;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Speech transcription was cancelled.");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during voice recognition.");
            throw new InvalidOperationException("Speech recognition error: " + ex.Message, ex);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            try
            {
                _recognizer?.Dispose();
            }
            catch
            {
                // Ignore cleanup errors
            }
            _initLock.Dispose();
        }
    }
}
