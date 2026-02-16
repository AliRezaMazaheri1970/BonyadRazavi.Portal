namespace BonyadRazavi.Shared.Contracts.Users;

public sealed class UserDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid CompanyCode { get; set; }
    public string? CompanyName { get; set; }
    public bool IsCompanyActive { get; set; }
    public List<string> Roles { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
}
