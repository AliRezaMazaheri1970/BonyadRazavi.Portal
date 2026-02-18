namespace BonyadRazavi.Auth.Api.Security;

public sealed class PasswordPolicyOptions
{
    public const string SectionName = "Security:PasswordPolicy";

    public int MinLength { get; set; } = 8;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireNonAlphanumeric { get; set; } = true;
    public string[] ForbiddenPasswords { get; set; } =
    [
        "123456",
        "12345678",
        "password",
        "qwerty",
        "admin",
        "رازى123",
        "رازى1234"
    ];
}
