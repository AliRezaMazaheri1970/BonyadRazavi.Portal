using System.ComponentModel.DataAnnotations;

namespace BonyadRazavi.Shared.Contracts.Account;

public sealed class ChangePasswordRequest
{
    [Required(ErrorMessage = "رمز فعلی الزامی است.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز جدید الزامی است.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "تکرار رمز جدید الزامی است.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
