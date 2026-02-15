namespace BonyadRazavi.Auth.Application.Models;

public sealed record AuthenticationResult(bool Succeeded, AuthenticatedUser? User, string? ErrorMessage)
{
    public static AuthenticationResult Success(AuthenticatedUser user) =>
        new(true, user, null);

    public static AuthenticationResult Fail(string errorMessage) =>
        new(false, null, errorMessage);
}
