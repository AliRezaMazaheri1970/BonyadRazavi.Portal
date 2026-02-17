namespace BonyadRazavi.Auth.Application.Models;

public sealed record RefreshTokenRevokeResult(bool Succeeded, string? ErrorMessage)
{
    public static RefreshTokenRevokeResult Success() => new(true, null);

    public static RefreshTokenRevokeResult Fail(string errorMessage) =>
        new(false, errorMessage);
}
