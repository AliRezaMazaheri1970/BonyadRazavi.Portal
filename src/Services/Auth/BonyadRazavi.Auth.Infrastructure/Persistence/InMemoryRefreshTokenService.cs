using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Models;
using Microsoft.Extensions.Options;

namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public sealed class InMemoryRefreshTokenService : IRefreshTokenService
{
    private readonly object _sync = new();
    private readonly Dictionary<string, RefreshTokenEntry> _entries = new(StringComparer.Ordinal);
    private readonly IOptions<RefreshTokenOptions> _options;

    public InMemoryRefreshTokenService(IOptions<RefreshTokenOptions> options)
    {
        _options = options;
    }

    public Task<RefreshTokenIssueResult> IssueAsync(
        AuthenticatedUser user,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var lifetimeDays = Math.Max(_options.Value.LifetimeDays, 1);
        var rawToken = RefreshTokenCrypto.GenerateToken();
        var hash = RefreshTokenCrypto.HashToken(rawToken);

        var entry = new RefreshTokenEntry(
            Id: Guid.NewGuid(),
            User: user,
            TokenHash: hash,
            TokenFamilyId: Guid.NewGuid(),
            CreatedAtUtc: now,
            ExpiresAtUtc: now.AddDays(lifetimeDays),
            RevokedAtUtc: null,
            RevocationReason: null);

        lock (_sync)
        {
            _entries[hash] = entry;
        }

        return Task.FromResult(new RefreshTokenIssueResult(rawToken, entry.ExpiresAtUtc));
    }

    public Task<RefreshTokenRotateResult> RotateAsync(
        string refreshToken,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Task.FromResult(RefreshTokenRotateResult.Fail("Refresh token معتبر نیست."));
        }

        var now = DateTime.UtcNow;
        var requestedHash = RefreshTokenCrypto.HashToken(refreshToken.Trim());
        RefreshTokenEntry? currentEntry;

        lock (_sync)
        {
            _entries.TryGetValue(requestedHash, out currentEntry);
            if (currentEntry is null)
            {
                return Task.FromResult(RefreshTokenRotateResult.Fail("Refresh token معتبر نیست."));
            }

            if (currentEntry.RevokedAtUtc is not null)
            {
                RevokeFamilyUnsafe(currentEntry.TokenFamilyId, now, "ReuseDetected");
                _entries[requestedHash] = currentEntry with
                {
                    RevokedAtUtc = currentEntry.RevokedAtUtc ?? now,
                    RevocationReason = currentEntry.RevocationReason ?? "ReuseDetected"
                };
                return Task.FromResult(RefreshTokenRotateResult.Fail("Refresh token معتبر نیست."));
            }

            if (currentEntry.ExpiresAtUtc <= now)
            {
                _entries[requestedHash] = currentEntry with
                {
                    RevokedAtUtc = now,
                    RevocationReason = "Expired"
                };
                return Task.FromResult(RefreshTokenRotateResult.Fail("Refresh token منقضی شده است."));
            }

            var lifetimeDays = Math.Max(_options.Value.LifetimeDays, 1);
            var newRawToken = RefreshTokenCrypto.GenerateToken();
            var newHash = RefreshTokenCrypto.HashToken(newRawToken);
            var newEntry = new RefreshTokenEntry(
                Id: Guid.NewGuid(),
                User: currentEntry.User,
                TokenHash: newHash,
                TokenFamilyId: currentEntry.TokenFamilyId,
                CreatedAtUtc: now,
                ExpiresAtUtc: now.AddDays(lifetimeDays),
                RevokedAtUtc: null,
                RevocationReason: null);

            _entries[requestedHash] = currentEntry with
            {
                RevokedAtUtc = now,
                RevocationReason = "Rotated"
            };
            _entries[newHash] = newEntry;

            return Task.FromResult(RefreshTokenRotateResult.Success(
                currentEntry.User,
                new RefreshTokenIssueResult(newRawToken, newEntry.ExpiresAtUtc)));
        }
    }

    public Task<RefreshTokenRevokeResult> RevokeAsync(
        string refreshToken,
        string? reason,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Task.FromResult(RefreshTokenRevokeResult.Fail("Refresh token معتبر نیست."));
        }

        var now = DateTime.UtcNow;
        var requestedHash = RefreshTokenCrypto.HashToken(refreshToken.Trim());

        lock (_sync)
        {
            if (!_entries.TryGetValue(requestedHash, out var entry))
            {
                return Task.FromResult(RefreshTokenRevokeResult.Fail("Refresh token یافت نشد."));
            }

            _entries[requestedHash] = entry with
            {
                RevokedAtUtc = now,
                RevocationReason = string.IsNullOrWhiteSpace(reason) ? "RevokedByRequest" : reason.Trim()
            };
        }

        return Task.FromResult(RefreshTokenRevokeResult.Success());
    }

    public Task<int> RevokeAllForUserAsync(
        Guid userId,
        string reason,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var revokedCount = 0;
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "RevokedByRequest" : reason.Trim();

        lock (_sync)
        {
            var keys = _entries
                .Where(pair =>
                    pair.Value.User.Id == userId &&
                    pair.Value.RevokedAtUtc is null &&
                    pair.Value.ExpiresAtUtc > now)
                .Select(pair => pair.Key)
                .ToList();

            foreach (var key in keys)
            {
                var entry = _entries[key];
                _entries[key] = entry with
                {
                    RevokedAtUtc = now,
                    RevocationReason = normalizedReason
                };
                revokedCount++;
            }
        }

        return Task.FromResult(revokedCount);
    }

    private void RevokeFamilyUnsafe(Guid familyId, DateTime now, string reason)
    {
        var keys = _entries
            .Where(pair => pair.Value.TokenFamilyId == familyId && pair.Value.RevokedAtUtc is null)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in keys)
        {
            var entry = _entries[key];
            _entries[key] = entry with
            {
                RevokedAtUtc = now,
                RevocationReason = reason
            };
        }
    }

    private sealed record RefreshTokenEntry(
        Guid Id,
        AuthenticatedUser User,
        string TokenHash,
        Guid TokenFamilyId,
        DateTime CreatedAtUtc,
        DateTime ExpiresAtUtc,
        DateTime? RevokedAtUtc,
        string? RevocationReason);
}
