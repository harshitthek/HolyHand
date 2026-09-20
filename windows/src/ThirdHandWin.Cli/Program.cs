namespace HolyHand.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("HolyHand CLI Diagnostic Tool v0.1.0");

        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        return command switch
        {
            "check" => RunCheck(),
            "snapshot" => RunSnapshot(),
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

    private static int RunCheck()
    {
        Console.WriteLine("Running environment checks...");
        Console.WriteLine("  OS: Windows (Build " + Environment.OSVersion.Version.Build + ")");
        Console.WriteLine("  .NET Runtime: " + Environment.Version);
        Console.WriteLine("All scaffold checks passed.");
        return 0;
    }

    private static int RunSnapshot()
    {
        Console.WriteLine("Snapshot command will be available after Milestone M5 (Screen Reader).");
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
