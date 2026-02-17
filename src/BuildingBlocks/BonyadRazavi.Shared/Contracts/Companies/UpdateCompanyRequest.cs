using System.ComponentModel.DataAnnotations;

namespace BonyadRazavi.Shared.Contracts.Companies;

public sealed class UpdateCompanyRequest
{
    [MaxLength(300)]
    public string? CompanyName { get; set; }

    public bool IsActive { get; set; } = true;
}
