using BonyadRazavi.Auth.Application.Models;

namespace BonyadRazavi.Auth.Application.Abstractions;

public interface IRefreshTokenService
{
    Task<RefreshTokenIssueResult> IssueAsync(
        AuthenticatedUser user,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<RefreshTokenRotateResult> RotateAsync(
        string refreshToken,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<RefreshTokenRevokeResult> RevokeAsync(
        string refreshToken,
        string? reason,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<int> RevokeAllForUserAsync(
        Guid userId,
        string reason,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default);
}
