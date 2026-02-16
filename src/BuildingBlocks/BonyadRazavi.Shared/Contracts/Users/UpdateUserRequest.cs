using System.ComponentModel.DataAnnotations;

namespace BonyadRazavi.Shared.Contracts.Users;

public sealed class UpdateUserRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [MinLength(8)]
    [MaxLength(128)]
    public string? Password { get; set; }

    [Required]
    [MinLength(1)]
    public List<string> Roles { get; set; } = [];

    [Required]
    public Guid CompanyCode { get; set; }

    [MaxLength(300)]
    public string? CompanyName { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsCompanyActive { get; set; } = true;
}
