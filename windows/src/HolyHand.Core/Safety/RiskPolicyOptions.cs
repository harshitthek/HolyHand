namespace HolyHand.Core.Safety;

public class RiskPolicyOptions
{
    // SensitiveVerbs: empty — Jarvis mode requires no confirmation dialogs for any safe action
    public static readonly IReadOnlySet<string> DefaultSensitiveVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Intentionally empty: zero friction for any safe action
    };

    // ProhibitedTerms: ONLY actual deletion operations — strictly enforced
    public static readonly IReadOnlySet<string> DefaultProhibitedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "delete",
        "deletion",
        "erase",
        "wipe",
        "destroy",
        "truncate",
        "format",
        "del"  // command-line deletion shorthand
    };

    // DenyListedProcesses: password managers that should never be automated
    public static readonly IReadOnlySet<string> DefaultDenyListedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "1password",
        "bitwarden",
        "keepass",
        "keepassxc",
        "lastpass",
        "dashlane",
        "enpass",
        "authenticator"
    };

    public HashSet<string> SensitiveVerbs { get; set; } = new(DefaultSensitiveVerbs, StringComparer.OrdinalIgnoreCase);

    public HashSet<string> ProhibitedTerms { get; set; } = new(DefaultProhibitedTerms, StringComparer.OrdinalIgnoreCase);

    public HashSet<string> DenyListedProcesses { get; set; } = new(DefaultDenyListedProcesses, StringComparer.OrdinalIgnoreCase);

    public ActionRiskScore EscalateOnRiskScore { get; set; } = ActionRiskScore.IrreversibleOrExternalEffect;

    // Disabled: no confirmation prompts for any safe action
    public bool RequireConfirmationOnSensitiveText { get; set; } = false;
}
