namespace BonyadRazavi.Auth.Domain.Entities;

public sealed class UserRefreshToken
{
    private UserRefreshToken()
    {
    }

    public UserRefreshToken(
        Guid id,
        Guid userId,
        Guid tokenFamilyId,
        string tokenHash,
        DateTime expiresAtUtc,
        string? createdByIp,
        string? createdByUserAgent,
        DateTime? createdAtUtc = null)
    {
        Id = id;
        UserId = userId;
        TokenFamilyId = tokenFamilyId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByIp = createdByIp;
        CreatedByUserAgent = createdByUserAgent;
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TokenFamilyId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public string? CreatedByIp { get; private set; }
    public string? CreatedByUserAgent { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? RevocationReason { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public UserAccount User { get; private set; } = null!;

    public bool IsActive(DateTime nowUtc)
    {
        return RevokedAtUtc is null && ExpiresAtUtc > nowUtc;
    }

    public void Revoke(DateTime nowUtc, string? revokedByIp, string? reason)
    {
        if (RevokedAtUtc is not null)
        {
            return;
        }

        RevokedAtUtc = nowUtc;
        RevokedByIp = string.IsNullOrWhiteSpace(revokedByIp) ? null : revokedByIp.Trim();
        RevocationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public void ReplaceWith(Guid replacedByTokenId)
    {
        ReplacedByTokenId = replacedByTokenId;
    }
}
