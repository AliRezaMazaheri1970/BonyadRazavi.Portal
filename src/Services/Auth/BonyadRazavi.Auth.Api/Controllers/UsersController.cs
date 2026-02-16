using BonyadRazavi.Auth.Api.Audit;
using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Constants;
using BonyadRazavi.Auth.Domain.Entities;
using BonyadRazavi.Auth.Infrastructure.Persistence;
using BonyadRazavi.Shared.Contracts.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BonyadRazavi.Auth.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public sealed class UsersController : ControllerBase
{
    private readonly AuthDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserActionLogService _userActionLogService;

    public UsersController(
        AuthDbContext dbContext,
        IPasswordHasher passwordHasher,
        IUserActionLogService userActionLogService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _userActionLogService = userActionLogService;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<UserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<UserDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .Include(user => user.Company)
            .OrderBy(user => user.UserName)
            .ToListAsync(cancellationToken);

        await WriteAuditLogAsync(
            ResolveActorUserId(),
            AuditActionTypes.ViewUsers,
            new Dictionary<string, object?>
            {
                ["resultCount"] = users.Count
            },
            cancellationToken);

        return Ok(users.Select(MapToDto).ToList());
    }

    [HttpGet("{userId:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetUser(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(account => account.Company)
            .FirstOrDefaultAsync(account => account.Id == userId, cancellationToken);

        if (user is null)
        {
            await WriteAuditLogAsync(
                ResolveActorUserId(),
                AuditActionTypes.ViewUser,
                new Dictionary<string, object?>
                {
                    ["targetUserId"] = userId,
                    ["found"] = false
                },
                cancellationToken);

            return NotFound();
        }

        await WriteAuditLogAsync(
            ResolveActorUserId(),
            AuditActionTypes.ViewUser,
            new Dictionary<string, object?>
            {
                ["targetUserId"] = user.Id,
                ["targetUserName"] = user.UserName,
                ["found"] = true
            },
            cancellationToken);

        return Ok(MapToDto(user));
    }

    [HttpPost]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (request.CompanyCode == Guid.Empty)
        {
            ModelState.AddModelError(nameof(request.CompanyCode), "CompanyCode is required.");
            return ValidationProblem(ModelState);
        }

        var normalizedUserName = request.UserName.Trim();
        var normalizedDisplayName = request.DisplayName.Trim();
        var roles = NormalizeRoles(request.Roles);

        if (roles.Count == 0)
        {
            ModelState.AddModelError(nameof(request.Roles), "At least one valid role is required.");
            return ValidationProblem(ModelState);
        }

        var userNameExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.UserName == normalizedUserName, cancellationToken);
        if (userNameExists)
        {
            await WriteAuditLogAsync(
                ResolveActorUserId(),
                AuditActionTypes.CreateUser,
                new Dictionary<string, object?>
                {
                    ["targetUserName"] = normalizedUserName,
                    ["status"] = "Conflict",
                    ["reason"] = "DuplicateUserName"
                },
                cancellationToken);

            return Conflict(new ProblemDetails
            {
                Title = "Duplicate UserName",
                Detail = $"UserName '{normalizedUserName}' already exists.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var user = new UserAccount(
            id: Guid.NewGuid(),
            userName: normalizedUserName,
            displayName: normalizedDisplayName,
            passwordHash: _passwordHasher.Hash(request.Password),
            roles: roles,
            isActive: request.IsActive);
        user.SetCompany(request.CompanyCode, request.CompanyName?.Trim(), request.IsCompanyActive);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditLogAsync(
            ResolveActorUserId(),
            AuditActionTypes.CreateUser,
            new Dictionary<string, object?>
            {
                ["targetUserId"] = user.Id,
                ["targetUserName"] = user.UserName,
                ["companyCode"] = user.Company?.CompanyCode,
                ["status"] = "Created"
            },
            cancellationToken);

        var response = MapToDto(user);
        return CreatedAtAction(nameof(GetUser), new { userId = response.UserId }, response);
    }

    [HttpPut("{userId:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> UpdateUser(
        Guid userId,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (request.CompanyCode == Guid.Empty)
        {
            ModelState.AddModelError(nameof(request.CompanyCode), "CompanyCode is required.");
            return ValidationProblem(ModelState);
        }

        var roles = NormalizeRoles(request.Roles);
        if (roles.Count == 0)
        {
            ModelState.AddModelError(nameof(request.Roles), "At least one valid role is required.");
            return ValidationProblem(ModelState);
        }

        var user = await _dbContext.Users
            .Include(account => account.Company)
            .FirstOrDefaultAsync(account => account.Id == userId, cancellationToken);
        if (user is null)
        {
            await WriteAuditLogAsync(
                ResolveActorUserId(),
                AuditActionTypes.UpdateUser,
                new Dictionary<string, object?>
                {
                    ["targetUserId"] = userId,
                    ["status"] = "NotFound"
                },
                cancellationToken);

            return NotFound();
        }

        user.UpdateProfile(request.DisplayName.Trim(), roles, request.IsActive);
        var passwordChanged = false;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.SetPasswordHash(_passwordHasher.Hash(request.Password));
            passwordChanged = true;
        }

        user.SetCompany(request.CompanyCode, request.CompanyName?.Trim(), request.IsCompanyActive);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditLogAsync(
            ResolveActorUserId(),
            AuditActionTypes.UpdateUser,
            new Dictionary<string, object?>
            {
                ["targetUserId"] = user.Id,
                ["targetUserName"] = user.UserName,
                ["companyCode"] = user.Company?.CompanyCode,
                ["passwordChanged"] = passwordChanged,
                ["status"] = "Updated"
            },
            cancellationToken);

        return Ok(MapToDto(user));
    }

    private static UserDto MapToDto(UserAccount user)
    {
        return new UserDto
        {
            UserId = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            IsActive = user.IsActive,
            CompanyCode = user.Company?.CompanyCode ?? Guid.Empty,
            CompanyName = user.Company?.CompanyName,
            IsCompanyActive = user.Company?.IsActive ?? false,
            Roles = user.Roles.ToList(),
            CreatedAtUtc = user.CreatedAtUtc
        };
    }

    private static List<string> NormalizeRoles(IEnumerable<string> roles)
    {
        return roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Guid? ResolveActorUserId()
    {
        return RequestAuditMetadataFactory.ResolveAuthenticatedUserId(User);
    }

    private Task WriteAuditLogAsync(
        Guid? userId,
        string actionType,
        IDictionary<string, object?> customValues,
        CancellationToken cancellationToken)
    {
        return _userActionLogService.LogAsync(
            userId,
            actionType,
            RequestAuditMetadataFactory.Create(HttpContext, customValues),
            cancellationToken);
    }
}
