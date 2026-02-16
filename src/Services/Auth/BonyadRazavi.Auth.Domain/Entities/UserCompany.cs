namespace BonyadRazavi.Auth.Domain.Entities;

public sealed class UserCompany
{
    private UserCompany()
    {
    }

    public UserCompany(
        Guid userId,
        Guid companyCode,
        string? companyName,
        bool isActive = true,
        DateTime? createdAtUtc = null)
    {
        UserId = userId;
        CompanyCode = companyCode;
        CompanyName = companyName;
        IsActive = isActive;
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
    }

    public Guid UserId { get; private set; }
    public Guid CompanyCode { get; private set; }
    public string? CompanyName { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public UserAccount User { get; private set; } = null!;

    public void Update(Guid companyCode, string? companyName, bool isActive)
    {
        CompanyCode = companyCode;
        CompanyName = companyName;
        IsActive = isActive;
    }
}
