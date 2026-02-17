namespace BonyadRazavi.Auth.Domain.Entities;

public sealed class UserAccount
{
    private UserAccount()
    {
    }

    public UserAccount(
        Guid id,
        string userName,
        string displayName,
        string passwordHash,
        IReadOnlyCollection<string> roles,
        bool isActive = true,
        DateTime? createdAtUtc = null)
    {
        Id = id;
        UserName = userName;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        Roles = roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        IsActive = isActive;
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public List<string> Roles { get; private set; } = [];
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public UserCompany? Company { get; private set; }
    public ICollection<UserActionLog> ActionLogs { get; private set; } = new List<UserActionLog>();
    public ICollection<UserRefreshToken> RefreshTokens { get; private set; } = new List<UserRefreshToken>();

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateProfile(string displayName, IReadOnlyCollection<string> roles, bool isActive)
    {
        DisplayName = displayName;
        Roles = roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetCompany(Guid companyCode, string? companyName, bool isActive)
    {
        if (Company is null)
        {
            Company = new UserCompany(Id, companyCode, companyName, isActive);
        }
        else
        {
            Company.Update(companyCode, companyName, isActive);
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }
}
