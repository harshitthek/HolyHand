using System.Diagnostics;
using HolyHand.Core.Common;
using HolyHand.Core.Jev;
using HolyHand.Core.Models;
using HolyHand.Core.ScreenReading;
using HolyHand.Platform.ScreenReading;
using HolyHand.Platform.Windowing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HolyHand.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Load local .env file if available
        EnvLoader.Load();

        Console.WriteLine("HolyHand CLI Diagnostic Tool v0.1.0");

        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        return command switch
        {
            "check" => await RunCheckAsync(),
            "snapshot" => await RunSnapshotAsync(args),
            "dry-run" => RunDryRun(args),
            _ => PrintUnknownCommand(command)
        };
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: HolyHand.Cli <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  check      Verify toolchain, credentials, and Jev connectivity");
        Console.WriteLine("  snapshot   Capture and display UIA element tree of frontmost window");
        Console.WriteLine("  dry-run    Run task in dry-run mode without executing actions");
    }

    private static async Task<int> RunCheckAsync()
    {
        Console.WriteLine("Running environment checks...");
        Console.WriteLine("  OS: Windows (Build " + Environment.OSVersion.Version.Build + ")");
        Console.WriteLine("  .NET Runtime: " + Environment.Version);

        var options = JevOptions.FromEnvironment();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  AI_GATEWAY_API_KEY: [NOT CONFIGURED] (Set in .env or as environment variable)");
            Console.ResetColor();
            Console.WriteLine("Cannot perform Jev evaluation without API key.");
            return 1;
        }

        var redacted = options.ApiKey.Length > 8 
            ? $"{options.ApiKey[..4]}...{options.ApiKey[^4..]}" 
            : "[CONFIGURED]";
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  AI_GATEWAY_API_KEY: {redacted} (Loaded)");
        Console.ResetColor();

        Console.WriteLine("\nTesting live Jev model via Vercel AI Gateway...");
        Console.WriteLine($"  Gateway Base URL: {options.BaseUrl}");
        Console.WriteLine($"  Model: {options.ModelId}");
        Console.WriteLine($"  Zero Data Retention: {options.ZeroDataRetention}");

        using var httpClient = new HttpClient();
        var client = new JevClient(httpClient, options, NullLogger<JevClient>.Instance);

        var request = new EvaluateRequest
        {
            Model = options.ModelId,
            State = new
            {
                system = "HolyHand Windows Diagnostic",
                status = "Testing connectivity to Vercel AI Gateway",
                timestamp = DateTime.UtcNow
            },
            Questions = new Dictionary<string, QuestionDefinition>
            {
                ["operational"] = QuestionDefinition.Boolean("Is this connection active and ready for evaluation?")
            },
            ProviderOptions = new GatewayProviderOptions
            {
                Gateway = new GatewayOptions
                {
                    ZeroDataRetention = options.ZeroDataRetention ? true : null,
                    Only = new List<string> { "typesafe-ai" }
                }
            }
        };

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await client.EvaluateAsync(request);
            sw.Stop();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[SUCCESS] Jev evaluation call returned successfully!");
            Console.ResetColor();

            Console.WriteLine($"  Latency: {sw.ElapsedMilliseconds}ms");

            if (response.TryGetBooleanAnswer("operational", out var prob, out var isTrue))
            {
                Console.WriteLine($"  Answer 'operational': {isTrue} (probability: {prob:P1})");
            }

            if (response.Usage != null)
            {
                Console.WriteLine($"  Tokens: {response.Usage.TotalTokens} (Prompt: {response.Usage.PromptTokens}, Completion: {response.Usage.CompletionTokens})");
            }

            var cost = response.ProviderMetadata?.Gateway?.Cost;
            if (cost.HasValue)
            {
                Console.WriteLine($"  Cost: ${cost.Value:F6}");
            }

            Console.WriteLine("\nAll diagnostic checks passed. Your API key and Gateway connection are fully verified.");
            return 0;
        }
        catch (AuthException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[AUTH ERROR] Authentication failed: {ex.Message}");
            Console.WriteLine("Please double-check that your AI_GATEWAY_API_KEY in .env is valid.");
            Console.ResetColor();
            return 1;
        }
        catch (TransientException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[GATEWAY ERROR] Gateway returned a transient error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] Unexpected error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> RunSnapshotAsync(string[] args)
    {
        Console.WriteLine("HolyHand Window Snapshot");
        Console.WriteLine("--------------------------------------------------------------------------------");

        AppTarget? target = null;
        if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
        {
            var query = args[1];
            if (int.TryParse(query, out var pid))
            {
                target = WindowCaptureService.CaptureWindowByProcessId(pid);
            }
            else
            {
                target = WindowCaptureService.CaptureWindowByProcessName(query);
            }

            if (target == null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Could not find active window for process '{query}'. Falling back to foreground window.");
                Console.ResetColor();
            }
        }

        if (target == null)
        {
            Console.WriteLine("Focus the window you wish to capture. Capturing in 2 seconds...");
            await Task.Delay(2000);
            target = WindowCaptureService.CaptureCurrentForegroundWindow();
        }

        if (target == null || target.WindowHandle == IntPtr.Zero)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Failed to capture target window.");
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Target Window: \"{target.WindowTitle}\"");
        Console.WriteLine($"Process: {target.ProcessName} (PID {target.ProcessId})");
        Console.WriteLine($"Bounds: {target.Bounds.Width}x{target.Bounds.Height} at ({target.Bounds.X}, {target.Bounds.Y})");
        Console.ResetColor();

        if (WindowCaptureService.IsTargetElevated(target.ProcessId))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n[SECURITY WARNING] Target application is running as Administrator (Elevated).");
            Console.WriteLine("UI Automation cannot inspect or interact with this window due to Windows UIPI.");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine("\nTraversing accessibility tree with CacheRequest...");
        var sw = Stopwatch.StartNew();

        var options = ScreenReaderOptions.Default;
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var ocrLogger = loggerFactory.CreateLogger<WindowsOcrService>();
        var readerLogger = loggerFactory.CreateLogger<UiaScreenReader>();

        var ocrService = new WindowsOcrService(ocrLogger);
        using var screenReader = new UiaScreenReader(options, ocrService, readerLogger);

        IReadOnlyList<AccessibilityElement> elements;
        try
        {
            elements = await screenReader.ReadElementsAsync(target);
            sw.Stop();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Snapshot failed: {ex.Message}");
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Completed in {sw.ElapsedMilliseconds}ms | Found {elements.Count} elements");
        Console.ResetColor();

        int ocrCount = elements.Count(e => e.Source == "ocr");
        int interactiveCount = elements.Count(e => ElementRanker.IsInteractive(e.Role));

        Console.WriteLine($"Interactive Controls: {interactiveCount} | OCR Elements: {ocrCount}");
        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.WriteLine($"{"ID",-5} | {"Role",-14} | {"State",-10} | {"Label / Text"}");
        Console.WriteLine("--------------------------------------------------------------------------------");

        foreach (var el in elements)
        {
            var state = el.Focused ? "[FOCUSED]" : (el.Enabled ? "Enabled" : "Disabled");
            var label = !string.IsNullOrWhiteSpace(el.DisplayLabel) ? el.DisplayLabel : "(empty)";
            if (label.Length > 60) label = label[..57] + "...";

            var color = el.Focused ? ConsoleColor.Yellow : (ElementRanker.IsInteractive(el.Role) ? ConsoleColor.Cyan : ConsoleColor.Gray);
            Console.ForegroundColor = color;
            Console.WriteLine($"{el.Id,-5} | {el.DisplayRole,-14} | {state,-10} | {label}");
        }

        Console.ResetColor();
        Console.WriteLine("--------------------------------------------------------------------------------");
        return 0;
    }

    private static int RunDryRun(string[] args)
    {
        var goal = args.Length > 1 ? args[1] : "sample goal";
        Console.WriteLine($"Dry-run mode: plan actions for goal '{goal}' (simulated)");
        return 0;
    }

    private static int PrintUnknownCommand(string command)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Unknown command: '{command}'");
        Console.ResetColor();
        PrintUsage();
        return 1;
    }
}
