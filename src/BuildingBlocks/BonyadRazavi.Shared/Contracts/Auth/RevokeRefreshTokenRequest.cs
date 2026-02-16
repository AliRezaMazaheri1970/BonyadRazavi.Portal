using System.ComponentModel.DataAnnotations;

namespace BonyadRazavi.Shared.Contracts.Auth;

public sealed class RevokeRefreshTokenRequest
{
    [Required(ErrorMessage = "Refresh token الزامی است.")]
    [MinLength(20, ErrorMessage = "Refresh token معتبر نیست.")]
    public string RefreshToken { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Reason { get; set; }
}
