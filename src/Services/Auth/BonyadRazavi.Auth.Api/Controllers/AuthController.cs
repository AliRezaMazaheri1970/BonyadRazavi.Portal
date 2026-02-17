using System.Globalization;
using System.Security.Claims;
using BonyadRazavi.Auth.Api.Audit;
using BonyadRazavi.Auth.Api.Security;
using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Constants;
using BonyadRazavi.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BonyadRazavi.Auth.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly JwtTokenFactory _jwtTokenFactory;
    private readonly ILoginLockoutService _loginLockoutService;
    private readonly IUserActionLogService _userActionLogService;

    public AuthController(
        IAuthenticationService authenticationService,
        IRefreshTokenService refreshTokenService,
        JwtTokenFactory jwtTokenFactory,
        ILoginLockoutService loginLockoutService,
        IUserActionLogService userActionLogService)
    {
        _authenticationService = authenticationService;
        _refreshTokenService = refreshTokenService;
        _jwtTokenFactory = jwtTokenFactory;
        _loginLockoutService = loginLockoutService;
        _userActionLogService = userActionLogService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userName = request.UserName?.Trim() ?? string.Empty;
        var clientIp = RequestAuditMetadataFactory.ResolveClientIp(HttpContext);
        var userAgent = Request.Headers.UserAgent.ToString();

        var lockoutStatus = _loginLockoutService.GetStatus(userName, clientIp);
        if (lockoutStatus.IsLocked)
        {
            await LogLoginFailureAsync(
                userName,
                reason: "LockedByRateLimit",
                lockoutStatus,
                cancellationToken);
            return BuildLockedResponse(lockoutStatus);
        }

        var result = await _authenticationService.AuthenticateAsync(request, cancellationToken);
        if (!result.Succeeded || result.User is null)
        {
            lockoutStatus = _loginLockoutService.RegisterFailure(userName, clientIp);
            await LogLoginFailureAsync(
                userName,
                reason: lockoutStatus.IsLocked ? "InvalidCredentialsLocked" : "InvalidCredentials",
                lockoutStatus,
                cancellationToken);
            if (lockoutStatus.IsLocked)
            {
                return BuildLockedResponse(lockoutStatus);
            }

            return Unauthorized(new ProblemDetails
            {
                Title = "ورود ناموفق",
                Detail = result.ErrorMessage,
                Status = StatusCodes.Status401Unauthorized
            });
        }

        _loginLockoutService.RegisterSuccess(userName, clientIp);
        var token = _jwtTokenFactory.Create(result.User);
        var refreshToken = await _refreshTokenService.IssueAsync(
            result.User,
            clientIp,
            userAgent,
            cancellationToken);

        token.RefreshToken = refreshToken.Token;
        token.RefreshTokenExpiresAtUtc = refreshToken.ExpiresAtUtc;

        await _userActionLogService.LogAsync(
            result.User.Id,
            AuditActionTypes.Login,
            RequestAuditMetadataFactory.Create(HttpContext, new Dictionary<string, object?>
            {
                ["userName"] = result.User.UserName,
                ["companyCode"] = result.User.CompanyCode,
                ["companyName"] = result.User.CompanyName,
                ["roles"] = result.User.Roles,
                ["accessTokenId"] = token.AccessTokenId
            }),
            cancellationToken);

        return Ok(token);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var clientIp = RequestAuditMetadataFactory.ResolveClientIp(HttpContext);
        var userAgent = Request.Headers.UserAgent.ToString();
        var rotateResult = await _refreshTokenService.RotateAsync(
            request.RefreshToken,
            clientIp,
            userAgent,
            cancellationToken);

        if (!rotateResult.Succeeded || rotateResult.User is null || rotateResult.RefreshToken is null)
        {
            await _userActionLogService.LogAsync(
                userId: null,
                actionType: AuditActionTypes.TokenRefresh,
                metadata: RequestAuditMetadataFactory.Create(HttpContext, new Dictionary<string, object?>
                {
                    ["isSuccess"] = false,
                    ["reason"] = rotateResult.ErrorMessage
                }),
                cancellationToken);

            return Unauthorized(new ProblemDetails
            {
                Title = "تمدید نشست ناموفق",
                Detail = rotateResult.ErrorMessage ?? "Refresh token معتبر نیست.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var response = _jwtTokenFactory.Create(rotateResult.User);
        response.RefreshToken = rotateResult.RefreshToken.Token;
        response.RefreshTokenExpiresAtUtc = rotateResult.RefreshToken.ExpiresAtUtc;

        await _userActionLogService.LogAsync(
            rotateResult.User.Id,
            AuditActionTypes.TokenRefresh,
            RequestAuditMetadataFactory.Create(HttpContext, new Dictionary<string, object?>
            {
                ["isSuccess"] = true,
                ["userName"] = rotateResult.User.UserName,
                ["companyCode"] = rotateResult.User.CompanyCode,
                ["accessTokenId"] = response.AccessTokenId
            }),
            cancellationToken);

        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Revoke(
        [FromBody] RevokeRefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var clientIp = RequestAuditMetadataFactory.ResolveClientIp(HttpContext);
        var userAgent = Request.Headers.UserAgent.ToString();
        var revokeResult = await _refreshTokenService.RevokeAsync(
            request.RefreshToken,
            request.Reason,
            clientIp,
            userAgent,
            cancellationToken);

        await _userActionLogService.LogAsync(
            RequestAuditMetadataFactory.ResolveAuthenticatedUserId(User),
            AuditActionTypes.TokenRevoke,
            RequestAuditMetadataFactory.Create(HttpContext, new Dictionary<string, object?>
            {
                ["isSuccess"] = revokeResult.Succeeded,
                ["reason"] = revokeResult.ErrorMessage ?? request.Reason
            }),
            cancellationToken);

        // Always return 204 to avoid token-existence probing.
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<object>> Me(CancellationToken cancellationToken)
    {
        var userId = RequestAuditMetadataFactory.ResolveAuthenticatedUserId(User);
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var displayName = User.FindFirstValue("display_name") ?? userName;
        var companyCode = User.FindFirstValue("company_code") ?? string.Empty;
        var companyName = User.FindFirstValue("company_name") ?? string.Empty;
        var roles = User.Claims
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToArray();

        await _userActionLogService.LogAsync(
            userId,
            AuditActionTypes.ViewProfile,
            RequestAuditMetadataFactory.Create(HttpContext, new Dictionary<string, object?>
            {
                ["userName"] = userName,
                ["companyCode"] = companyCode
            }),
            cancellationToken);

        return Ok(new
        {
            userName,
            displayName,
            companyCode,
            companyName,
            roles
        });
    }

    private ActionResult<LoginResponse> BuildLockedResponse(LockoutStatus lockoutStatus)
    {
        var retryAfterSeconds = Math.Max((int)Math.Ceiling(lockoutStatus.RetryAfter.TotalSeconds), 1);
        var retryAfterMinutes = Math.Max((int)Math.Ceiling(lockoutStatus.RetryAfter.TotalMinutes), 1);

        Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        return StatusCode(StatusCodes.Status423Locked, new ProblemDetails
        {
            Title = "ورود موقتا مسدود شد",
            Detail = $"به دلیل تلاش ناموفق مکرر، ورود تا {retryAfterMinutes} دقیقه دیگر مسدود است.",
            Status = StatusCodes.Status423Locked
        });
    }

    private async Task LogLoginFailureAsync(
        string userName,
        string reason,
        LockoutStatus lockoutStatus,
        CancellationToken cancellationToken)
    {
        await _userActionLogService.LogAsync(
            userId: null,
            actionType: AuditActionTypes.LoginFailed,
            metadata: RequestAuditMetadataFactory.Create(HttpContext, new Dictionary<string, object?>
            {
                ["userName"] = userName,
                ["reason"] = reason,
                ["isLocked"] = lockoutStatus.IsLocked,
                ["retryAfterSeconds"] = lockoutStatus.IsLocked
                    ? Math.Max((int)Math.Ceiling(lockoutStatus.RetryAfter.TotalSeconds), 1)
                    : null
            }),
            cancellationToken);
    }
}
