namespace BonyadRazavi.Auth.Application.Models;

public sealed record RefreshTokenRotateResult(
    bool Succeeded,
    AuthenticatedUser? User,
    RefreshTokenIssueResult? RefreshToken,
    string? ErrorMessage)
{
    public static RefreshTokenRotateResult Success(
        AuthenticatedUser user,
        RefreshTokenIssueResult refreshToken) =>
        new(true, user, refreshToken, null);

    public static RefreshTokenRotateResult Fail(string errorMessage) =>
        new(false, null, null, errorMessage);
}
