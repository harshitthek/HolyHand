using System.Drawing;
using System.Drawing.Imaging;
using HolyHand.Core.Models;
using HolyHand.Core.ScreenReading;
using Microsoft.Extensions.Logging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace HolyHand.Platform.ScreenReading;

public class WindowsOcrService
{
    private readonly ILogger<WindowsOcrService> _logger;
    private OcrEngine? _ocrEngine;

    public WindowsOcrService(ILogger<WindowsOcrService> logger)
    {
        _logger = logger;
        InitializeEngine();
    }

    private void InitializeEngine()
    {
        try
        {
            _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages()
                ?? (OcrEngine.AvailableRecognizerLanguages.Count > 0 
                    ? OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]) 
                    : null);

            if (_ocrEngine == null)
            {
                _logger.LogWarning("Windows OCR engine could not be initialized. No installed OCR language found.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize Windows.Media.Ocr engine.");
        }
    }

    public async Task<IReadOnlyList<AccessibilityElement>> RecognizeScreenAreaAsync(
        Rectangle bounds,
        CancellationToken cancellationToken = default)
    {
        if (_ocrEngine == null || bounds.Width <= 0 || bounds.Height <= 0)
            return Array.Empty<AccessibilityElement>();

        try
        {
            using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, new Size(bounds.Width, bounds.Height), CopyPixelOperation.SourceCopy);
            }

            return await RecognizeBitmapAsync(bitmap, bounds.Location, cancellationToken);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogDebug(ex, "Could not capture off-screen area {Bounds} for OCR.", bounds);
            return Array.Empty<AccessibilityElement>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error executing Windows OCR on screen area {Bounds}", bounds);
            return Array.Empty<AccessibilityElement>();
        }
    }

    public async Task<IReadOnlyList<AccessibilityElement>> RecognizeBitmapAsync(
        Bitmap bitmap,
        Point screenOffset,
        CancellationToken cancellationToken = default)
    {
        if (_ocrEngine == null)
            return Array.Empty<AccessibilityElement>();

        try
        {
            using var stream = new InMemoryRandomAccessStream();
            using (var netStream = stream.AsStreamForWrite())
            {
                bitmap.Save(netStream, ImageFormat.Bmp);
                await netStream.FlushAsync(cancellationToken);
            }

            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            var result = await _ocrEngine.RecognizeAsync(softwareBitmap);
            if (result == null || result.Lines == null)
                return Array.Empty<AccessibilityElement>();

            var elements = new List<AccessibilityElement>();
            int elementIndex = 1;

            foreach (var line in result.Lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var lineText = line.Text?.Trim();
                if (string.IsNullOrWhiteSpace(lineText))
                    continue;

                // Create element for the full line
                var lineRect = new Rectangle(
                    (int)line.Words.Min(w => w.BoundingRect.X) + screenOffset.X,
                    (int)line.Words.Min(w => w.BoundingRect.Y) + screenOffset.Y,
                    (int)line.Words.Sum(w => w.BoundingRect.Width),
                    (int)line.Words.Max(w => w.BoundingRect.Height)
                );

                elements.Add(new AccessibilityElement
                {
                    Id = $"ocr_{elementIndex++}",
                    Role = "Text",
                    Label = SecretSanitizer.Sanitize(lineText),
                    Frame = lineRect,
                    Source = "ocr",
                    Enabled = true
                });
            }

            return elements;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Windows OCR recognition failed on bitmap.");
            return Array.Empty<AccessibilityElement>();
        }
    }
}
