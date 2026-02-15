namespace BonyadRazavi.Auth.Api.Security;

public sealed class LoginLockoutOptions
{
    public const string SectionName = "Security:LoginLockout";

    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int EntryTtlMinutes { get; set; } = 120;
}
