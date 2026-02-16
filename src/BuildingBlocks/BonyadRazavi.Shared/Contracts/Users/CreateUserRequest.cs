using System.ComponentModel.DataAnnotations;

namespace BonyadRazavi.Shared.Contracts.Users;

public sealed class CreateUserRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [MinLength(3)]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

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
