using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Models;
using BonyadRazavi.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public sealed class DbRefreshTokenService : IRefreshTokenService
{
    private readonly AuthDbContext _dbContext;
    private readonly ICompanyDirectoryService _companyDirectoryService;
    private readonly IOptions<RefreshTokenOptions> _options;
    private readonly ILogger<DbRefreshTokenService> _logger;

    public DbRefreshTokenService(
        AuthDbContext dbContext,
        ICompanyDirectoryService companyDirectoryService,
        IOptions<RefreshTokenOptions> options,
        ILogger<DbRefreshTokenService> logger)
    {
        _dbContext = dbContext;
        _companyDirectoryService = companyDirectoryService;
        _options = options;
        _logger = logger;
    }

    public async Task<RefreshTokenIssueResult> IssueAsync(
        AuthenticatedUser user,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var lifetimeDays = GetLifetimeDays();
        var rawToken = RefreshTokenCrypto.GenerateToken();
        var tokenEntity = new UserRefreshToken(
            id: Guid.NewGuid(),
            userId: user.Id,
            tokenFamilyId: Guid.NewGuid(),
            tokenHash: RefreshTokenCrypto.HashToken(rawToken),
            expiresAtUtc: now.AddDays(lifetimeDays),
            createdByIp: NormalizeIp(clientIp),
            createdByUserAgent: NormalizeUserAgent(userAgent),
            createdAtUtc: now);

        _dbContext.UserRefreshTokens.Add(tokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RefreshTokenIssueResult(rawToken, tokenEntity.ExpiresAtUtc);
    }

    public async Task<RefreshTokenRotateResult> RotateAsync(
        string refreshToken,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return RefreshTokenRotateResult.Fail("Refresh token معتبر نیست.");
        }

        var now = DateTime.UtcNow;
        var tokenHash = RefreshTokenCrypto.HashToken(refreshToken.Trim());
        var refreshTokenEntity = await _dbContext.UserRefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (refreshTokenEntity is null)
        {
            return RefreshTokenRotateResult.Fail("Refresh token معتبر نیست.");
        }

        if (refreshTokenEntity.RevokedAtUtc is not null)
        {
            await RevokeFamilyAsync(
                refreshTokenEntity.TokenFamilyId,
                now,
                NormalizeIp(clientIp),
                reason: "ReuseDetected",
                cancellationToken);

            return RefreshTokenRotateResult.Fail("Refresh token معتبر نیست.");
        }

        if (refreshTokenEntity.ExpiresAtUtc <= now)
        {
            refreshTokenEntity.Revoke(now, NormalizeIp(clientIp), "Expired");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return RefreshTokenRotateResult.Fail("Refresh token منقضی شده است.");
        }

        var user = refreshTokenEntity.User;
        if (!user.IsActive || user.CompanyCode == Guid.Empty)
        {
            refreshTokenEntity.Revoke(now, NormalizeIp(clientIp), "InactiveUser");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return RefreshTokenRotateResult.Fail("حساب کاربری فعال نیست.");
        }

        var lifetimeDays = GetLifetimeDays();
        var newRawToken = RefreshTokenCrypto.GenerateToken();
        var newToken = new UserRefreshToken(
            id: Guid.NewGuid(),
            userId: user.Id,
            tokenFamilyId: refreshTokenEntity.TokenFamilyId,
            tokenHash: RefreshTokenCrypto.HashToken(newRawToken),
            expiresAtUtc: now.AddDays(lifetimeDays),
            createdByIp: NormalizeIp(clientIp),
            createdByUserAgent: NormalizeUserAgent(userAgent),
            createdAtUtc: now);

        refreshTokenEntity.Revoke(now, NormalizeIp(clientIp), "Rotated");
        refreshTokenEntity.ReplaceWith(newToken.Id);

        _dbContext.UserRefreshTokens.Add(newToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var company = await _companyDirectoryService.FindByCodeAsync(user.CompanyCode, cancellationToken);
        var authenticatedUser = new AuthenticatedUser(
            user.Id,
            user.UserName,
            user.DisplayName,
            user.Roles,
            user.CompanyCode,
            company?.CompanyName);

        return RefreshTokenRotateResult.Success(
            authenticatedUser,
            new RefreshTokenIssueResult(newRawToken, newToken.ExpiresAtUtc));
    }

    public async Task<RefreshTokenRevokeResult> RevokeAsync(
        string refreshToken,
        string? reason,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return RefreshTokenRevokeResult.Fail("Refresh token معتبر نیست.");
        }

        var tokenHash = RefreshTokenCrypto.HashToken(refreshToken.Trim());
        var refreshTokenEntity = await _dbContext.UserRefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (refreshTokenEntity is null)
        {
            return RefreshTokenRevokeResult.Fail("Refresh token یافت نشد.");
        }

        var now = DateTime.UtcNow;
        refreshTokenEntity.Revoke(
            now,
            NormalizeIp(clientIp),
            string.IsNullOrWhiteSpace(reason) ? "RevokedByRequest" : reason);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return RefreshTokenRevokeResult.Success();
    }

    private async Task RevokeFamilyAsync(
        Guid tokenFamilyId,
        DateTime now,
        string? revokedByIp,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            var activeFamilyTokens = await _dbContext.UserRefreshTokens
                .Where(token =>
                    token.TokenFamilyId == tokenFamilyId &&
                    token.RevokedAtUtc == null &&
                    token.ExpiresAtUtc > now)
                .ToListAsync(cancellationToken);

            if (activeFamilyTokens.Count == 0)
            {
                return;
            }

            foreach (var token in activeFamilyTokens)
            {
                token.Revoke(now, revokedByIp, reason);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to revoke refresh token family {TokenFamilyId}.",
                tokenFamilyId);
        }
    }

    private int GetLifetimeDays()
    {
        return Math.Max(_options.Value.LifetimeDays, 1);
    }

    private static string? NormalizeIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return null;
        }

        var value = ip.Trim();
        return value.Length <= 45 ? value : value[..45];
    }

    private static string? NormalizeUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var value = userAgent.Trim();
        return value.Length <= 500 ? value : value[..500];
    }
}
