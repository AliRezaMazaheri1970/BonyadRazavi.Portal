using BonyadRazavi.Auth.Api.Audit;
using BonyadRazavi.Auth.Api.Security;
using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Constants;
using BonyadRazavi.Shared.Contracts.Account;
using BonyadRazavi.Shared.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace BonyadRazavi.Auth.Api.Controllers;

[ApiController]
[Route("api/account")]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
public sealed class AccountController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IUserActionLogService _userActionLogService;
    private readonly PasswordPolicyOptions _passwordPolicyOptions;

    public AccountController(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService,
        IUserActionLogService userActionLogService,
        IOptions<PasswordPolicyOptions> passwordPolicyOptions)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _refreshTokenService = refreshTokenService;
        _userActionLogService = userActionLogService;
        _passwordPolicyOptions = passwordPolicyOptions.Value;
    }

    [HttpPost("change-password")]
    [EnableRateLimiting("change-password")]
    [ProducesResponseType<ApiResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResult>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = RequestAuditMetadataFactory.ResolveAuthenticatedUserId(User);
        if (userId is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "احراز هویت نامعتبر",
                Detail = "نشست کاربری معتبر نیست. لطفا دوباره وارد شوید.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var user = await _userRepository.FindByUserIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "کاربر نامعتبر",
                Detail = "کاربر جاری معتبر نیست. لطفا دوباره وارد شوید.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        if (!_passwordHasher.Verify(user.PasswordHash, request.CurrentPassword))
        {
            ModelState.AddModelError(nameof(request.CurrentPassword), "رمز فعلی صحیح نیست.");
            return ValidationProblem(ModelState);
        }

        if (!string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(request.ConfirmNewPassword), "تکرار رمز جدید با رمز جدید یکسان نیست.");
        }

        if (_passwordHasher.Verify(user.PasswordHash, request.NewPassword))
        {
            ModelState.AddModelError(nameof(request.NewPassword), "رمز جدید نباید با رمز فعلی یکسان باشد.");
        }

        foreach (var error in ValidatePasswordPolicy(request.NewPassword, user.UserName))
        {
            ModelState.AddModelError(nameof(request.NewPassword), error);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        user.SetPasswordHash(_passwordHasher.Hash(request.NewPassword));
        await _userRepository.SaveChangesAsync(cancellationToken);

        var revokedTokensCount = await _refreshTokenService.RevokeAllForUserAsync(
            user.Id,
            reason: "PasswordChanged",
            clientIp: RequestAuditMetadataFactory.ResolveClientIp(HttpContext),
            userAgent: Request.Headers.UserAgent.ToString(),
            cancellationToken: cancellationToken);

        await _userActionLogService.LogAsync(
            user.Id,
            AuditActionTypes.ChangePassword,
            RequestAuditMetadataFactory.Create(HttpContext, new Dictionary<string, object?>
            {
                ["revokedRefreshTokenCount"] = revokedTokensCount
            }),
            cancellationToken);

        return Ok(ApiResult.Ok("رمز عبور با موفقیت تغییر یافت. برای امنیت بیشتر نشست‌های قبلی لغو شدند."));
    }

    private IReadOnlyCollection<string> ValidatePasswordPolicy(string newPassword, string userName)
    {
        var errors = new List<string>();
        var candidate = newPassword ?? string.Empty;
        var options = _passwordPolicyOptions;

        if (candidate.Length < Math.Max(options.MinLength, 1))
        {
            errors.Add($"رمز جدید باید حداقل {Math.Max(options.MinLength, 1)} کاراکتر باشد.");
        }

        if (options.RequireLowercase && !candidate.Any(char.IsLower))
        {
            errors.Add("رمز جدید باید حداقل یک حرف کوچک انگلیسی داشته باشد.");
        }

        if (options.RequireUppercase && !candidate.Any(char.IsUpper))
        {
            errors.Add("رمز جدید باید حداقل یک حرف بزرگ انگلیسی داشته باشد.");
        }

        if (options.RequireDigit && !candidate.Any(char.IsDigit))
        {
            errors.Add("رمز جدید باید حداقل یک رقم داشته باشد.");
        }

        if (options.RequireNonAlphanumeric && candidate.All(char.IsLetterOrDigit))
        {
            errors.Add("رمز جدید باید حداقل یک کاراکتر غیرحرفی داشته باشد.");
        }

        if (!string.IsNullOrWhiteSpace(userName) &&
            candidate.Contains(userName, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("رمز جدید نباید شامل نام کاربری باشد.");
        }

        if (options.ForbiddenPasswords.Any(value =>
                string.Equals(value?.Trim(), candidate, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("رمز انتخابی در لیست رمزهای غیرمجاز قرار دارد.");
        }

        return errors;
    }
}
