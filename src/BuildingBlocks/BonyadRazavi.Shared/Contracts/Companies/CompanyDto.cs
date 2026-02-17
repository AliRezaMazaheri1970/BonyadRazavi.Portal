namespace BonyadRazavi.Shared.Contracts.Companies;

public sealed class CompanyDto
{
    public Guid CompanyCode { get; set; }
    public string? CompanyName { get; set; }
    public bool IsActive { get; set; }
    public int UsersCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
