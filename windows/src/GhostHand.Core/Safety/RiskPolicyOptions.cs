namespace HolyHand.Core.Safety;

public class RiskPolicyOptions
{
    public static readonly IReadOnlySet<string> DefaultSensitiveVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "submit",
        "apply",
        "send",
        "pay",
        "buy",
        "purchase",
        "order",
        "delete",
        "remove",
        "post",
        "publish",
        "confirm",
        "sign in",
        "signin",
        "log in",
        "login",
        "install",
        "run",
        "uninstall",
        "transfer"
    };

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

    public HashSet<string> DenyListedProcesses { get; set; } = new(DefaultDenyListedProcesses, StringComparer.OrdinalIgnoreCase);

    public ActionRiskScore EscalateOnRiskScore { get; set; } = ActionRiskScore.IrreversibleOrExternalEffect;

    public bool RequireConfirmationOnSensitiveText { get; set; } = true;
}
