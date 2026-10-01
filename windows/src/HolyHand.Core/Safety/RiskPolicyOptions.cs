namespace HolyHand.Core.Safety;

public class RiskPolicyOptions
{
    // SensitiveVerbs: actions requiring human confirmation before execution
    public static readonly IReadOnlySet<string> DefaultSensitiveVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Financial & transactional
        "pay",
        "buy",
        "purchase",
        "order",
        "checkout",
        "transfer",
        "subscribe",
        "tip",
        // Submission & publishing
        "submit",
        "apply",
        "send",
        "post",
        "publish",
        "confirm",
        "file",
        "register",
        // System & lifecycle
        "install",
        "uninstall",
        "overwrite",
        "reboot",
        "shutdown"
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

    // Enforce confirmation prompts on sensitive actions and text
    public bool RequireConfirmationOnSensitiveText { get; set; } = true;
}

