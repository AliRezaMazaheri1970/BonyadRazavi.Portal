using System.Globalization;
using System.Net;
using System.Security.Claims;
using BonyadRazavi.Auth.Api.Security;
using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BonyadRazavi.Auth.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly JwtTokenFactory _jwtTokenFactory;
    private readonly ILoginLockoutService _loginLockoutService;

    public AuthController(
        IAuthenticationService authenticationService,
        JwtTokenFactory jwtTokenFactory,
        ILoginLockoutService loginLockoutService)
    {
        _authenticationService = authenticationService;
        _jwtTokenFactory = jwtTokenFactory;
        _loginLockoutService = loginLockoutService;
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

        var userName = request.UserName ?? string.Empty;
        var clientIp = ResolveClientIp(HttpContext);

        var lockoutStatus = _loginLockoutService.GetStatus(userName, clientIp);
        if (lockoutStatus.IsLocked)
        {
            return BuildLockedResponse(lockoutStatus);
        }

        var result = await _authenticationService.AuthenticateAsync(request, cancellationToken);
        if (!result.Succeeded || result.User is null)
        {
            lockoutStatus = _loginLockoutService.RegisterFailure(userName, clientIp);
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
        return Ok(token);
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<object> Me()
    {
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var displayName = User.FindFirstValue("display_name") ?? userName;
        var roles = User.Claims
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToArray();

        return Ok(new
        {
            userName,
            displayName,
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

    private static string ResolveClientIp(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedForHeader))
        {
            var firstValue = forwardedForHeader
                .ToString()
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(firstValue) && IPAddress.TryParse(firstValue, out _))
            {
                return firstValue;
            }
        }

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is not null)
        {
            if (remoteIp.IsIPv4MappedToIPv6)
            {
                remoteIp = remoteIp.MapToIPv4();
            }

            return remoteIp.ToString();
        }

        return "unknown-ip";
    }
}
