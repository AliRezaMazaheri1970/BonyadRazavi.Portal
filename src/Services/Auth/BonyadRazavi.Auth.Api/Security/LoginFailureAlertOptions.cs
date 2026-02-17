namespace BonyadRazavi.Auth.Api.Security;

public sealed class LoginFailureAlertOptions
{
    public const string SectionName = "Security:Alerts";

    public int FailedAttemptsThreshold { get; set; } = 20;
    public int WindowMinutes { get; set; } = 10;
    public int SuppressionMinutes { get; set; } = 15;
}
