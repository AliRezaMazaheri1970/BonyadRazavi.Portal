using System.ComponentModel.DataAnnotations;

namespace BonyadRazavi.Shared.Contracts.Auth;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "نام کاربری الزامی است.")]
    [MinLength(3, ErrorMessage = "نام کاربری باید حداقل 3 کاراکتر باشد.")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "کلمه عبور الزامی است.")]
    [MinLength(6, ErrorMessage = "کلمه عبور باید حداقل 6 کاراکتر باشد.")]
    public string Password { get; set; } = string.Empty;
}
