namespace HolyHand.Core.Agent;

public record AgentLoopOptions
{
    public int MaxSteps { get; init; } = 30;
    public bool DryRun { get; init; } = true;
    public int MaxConsecutiveStalls { get; init; } = 3;
    public int ActionTimeoutSeconds { get; init; } = 10;

    public static AgentLoopOptions FromEnvironment()
    {
        var options = new AgentLoopOptions();

        if (bool.TryParse(Environment.GetEnvironmentVariable("DRY_RUN"), out var dryRun))
            options = options with { DryRun = dryRun };

        if (int.TryParse(Environment.GetEnvironmentVariable("MAX_STEPS_PER_RUN"), out var maxSteps))
            options = options with { MaxSteps = maxSteps };

        return options;
    }
}
