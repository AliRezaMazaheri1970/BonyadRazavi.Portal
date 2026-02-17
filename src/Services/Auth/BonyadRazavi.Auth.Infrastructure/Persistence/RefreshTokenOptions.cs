namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "Security:RefreshTokens";

    public int LifetimeDays { get; set; } = 7;
}
