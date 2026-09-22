using System.Drawing;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Models;
using HolyHand.Core.ScreenReading;
using HolyHand.Platform.Windowing;
using Microsoft.Extensions.Logging;

namespace HolyHand.Platform.ScreenReading;

public class UiaScreenReader : IScreenReader, IDisposable
{
    private readonly ScreenReaderOptions _options;
    private readonly WindowsOcrService? _ocrService;
    private readonly ILogger<UiaScreenReader> _logger;
    private readonly UIA3Automation _automation;
    private bool _disposed;

    public UiaScreenReader(
        ScreenReaderOptions options,
        WindowsOcrService? ocrService,
        ILogger<UiaScreenReader> logger)
    {
        _options = options;
        _ocrService = ocrService;
        _logger = logger;
        _automation = new UIA3Automation();
    }

    public async Task<IReadOnlyList<AccessibilityElement>> ReadElementsAsync(
        AppTarget target,
        CancellationToken cancellationToken = default)
    {
        if (target.WindowHandle == IntPtr.Zero)
        {
            throw new ArgumentException("Cannot read screen: invalid window handle (IntPtr.Zero).", nameof(target));
        }

        // 1. UIPI security check: refuse elevated targets
        if (WindowCaptureService.IsTargetElevated(target.ProcessId))
        {
            throw new InvalidOperationException(
                $"Target application '{target.ProcessName}' (PID {target.ProcessId}) is running with elevated privileges (Administrator). UI Automation is blocked by Windows UIPI.");
        }

        return await Task.Run(async () =>
        {
            var rawElements = new List<AccessibilityElement>();

            try
            {
                // 2. Attach to target window HWND
                var root = _automation.FromHandle(target.WindowHandle);
                if (root == null)
                {
                    _logger.LogWarning("UIA returned null automation element for window handle {Hwnd}", target.WindowHandle);
                    return Array.Empty<AccessibilityElement>();
                }

                // 3. Configure CacheRequest for batch prefetching
                var cacheRequest = new CacheRequest();
                cacheRequest.AutomationElementMode = AutomationElementMode.Full;
                cacheRequest.Add(_automation.PropertyLibrary.Element.Name);
                cacheRequest.Add(_automation.PropertyLibrary.Element.ControlType);
                cacheRequest.Add(_automation.PropertyLibrary.Element.IsEnabled);
                cacheRequest.Add(_automation.PropertyLibrary.Element.BoundingRectangle);
                cacheRequest.Add(_automation.PropertyLibrary.Element.IsKeyboardFocusable);
                cacheRequest.Add(_automation.PropertyLibrary.Element.HasKeyboardFocus);
                cacheRequest.Add(_automation.PropertyLibrary.Element.IsPassword);
                cacheRequest.Add(_automation.PropertyLibrary.Element.IsOffscreen);
                cacheRequest.TreeScope = TreeScope.Subtree;

                // 4. Batch fetch descendants with active cache request
                AutomationElement[] descendants;
                using (cacheRequest.Activate())
                {
                    descendants = root.FindAllDescendants() ?? Array.Empty<AutomationElement>();
                }

                var allElements = new List<AutomationElement>(descendants.Length + 1) { root };
                allElements.AddRange(descendants);

                int count = 0;
                foreach (var el in allElements)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (count++ >= _options.MaxNodes)
                    {
                        _logger.LogDebug("Reached MaxNodes cap ({Cap}). Halting UIA traversal.", _options.MaxNodes);
                        break;
                    }

                    var accEl = ExtractElement(el);
                    if (accEl != null)
                    {
                        rawElements.Add(accEl);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception encountered during UIA traversal for target {Target}", target.ProcessName);
            }

            // 5. Evaluate UIA coverage: fallback to OCR if interactive element count is low
            int interactiveCount = rawElements.Count(e => ElementRanker.IsInteractive(e.Role));
            if ((rawElements.Count == 0 || (_options.OcrFallbackThreshold > 0 && interactiveCount < _options.OcrFallbackThreshold)) && _ocrService != null)
            {
                _logger.LogInformation(
                    "Interactive UIA element count ({Count}) requires fallback. Activating local OCR.",
                    interactiveCount);

                try
                {
                    var ocrElements = await _ocrService.RecognizeScreenAreaAsync(target.Bounds, cancellationToken);
                    rawElements.AddRange(ocrElements);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OCR fallback encountered an error.");
                }
            }

            // 6. Filter, prioritize, and assign stable IDs
            return ElementRanker.RankAndFilter(rawElements, _options);
        }, cancellationToken);
    }

    private AccessibilityElement? ExtractElement(AutomationElement element)
    {
        try
        {
            var controlType = element.Properties.ControlType.ValueOrDefault;
            var role = controlType.ToString();

            // Skip uninteresting structural containers with no name
            var name = string.Empty;
            try { name = element.Properties.Name.ValueOrDefault ?? string.Empty; } catch { }

            var isPassword = false;
            try { isPassword = element.Properties.IsPassword.ValueOrDefault; } catch { }

            var val = string.Empty;
            if (!isPassword)
            {
                try
                {
                    if (element.Patterns.Value.IsSupported)
                    {
                        val = element.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault ?? string.Empty;
                    }
                }
                catch { }
            }

            // Sanitization: passwords and secrets never escape
            var label = SecretSanitizer.Sanitize(name, isPassword);
            var value = SecretSanitizer.Sanitize(val, isPassword);

            // If an element is a generic container without name, value, or interactivity, skip
            if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(value) && !ElementRanker.IsInteractive(role))
            {
                return null;
            }

            var enabled = true;
            try { enabled = element.Properties.IsEnabled.ValueOrDefault; } catch { }

            var focused = false;
            try { focused = element.Properties.HasKeyboardFocus.ValueOrDefault; } catch { }

            var frame = Rectangle.Empty;
            try
            {
                var rect = element.Properties.BoundingRectangle.ValueOrDefault;
                if (!rect.IsEmpty)
                {
                    frame = new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                }
            }
            catch { }

            var isOffscreen = false;
            try { isOffscreen = element.Properties.IsOffscreen.ValueOrDefault; } catch { }

            if (_options.FilterOffscreen && isOffscreen && frame.Width <= 0 && frame.Height <= 0)
                return null;

            var actions = new List<string>();
            try
            {
                if (element.Patterns.Invoke.IsSupported) actions.Add("click");
                if (element.Patterns.Value.IsSupported) actions.Add("type");
                if (element.Patterns.Toggle.IsSupported) actions.Add("toggle");
                if (element.Patterns.Scroll.IsSupported) actions.Add("scroll");
            }
            catch { }

            return new AccessibilityElement
            {
                Id = string.Empty, // Will be stably assigned by ElementRanker
                Role = role,
                Label = label,
                Value = value,
                Enabled = enabled,
                Focused = focused,
                Frame = frame,
                Source = "accessibility",
                Actions = actions
            };
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to extract properties from element.");
            return null;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _automation.Dispose();
        }
    }
}
