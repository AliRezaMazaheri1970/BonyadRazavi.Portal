using BonyadRazavi.Auth.Api.Audit;
using BonyadRazavi.Auth.Api.Security;
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
[Authorize(Policy = AuthorizationPolicies.PortalAccess)]
public sealed class UsersController : ControllerBase
{
    private readonly AuthDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICompanyDirectoryService _companyDirectoryService;
    private readonly IUserActionLogService _userActionLogService;

    public UsersController(
        AuthDbContext dbContext,
        IPasswordHasher passwordHasher,
        ICompanyDirectoryService companyDirectoryService,
        IUserActionLogService userActionLogService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _companyDirectoryService = companyDirectoryService;
        _userActionLogService = userActionLogService;
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.UsersRead)]
    [ProducesResponseType<IReadOnlyCollection<UserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<UserDto>>> GetUsers(CancellationToken cancellationToken)
    {
        IQueryable<UserAccount> query = _dbContext.Users.AsNoTracking();

        if (!CanBypassTenantIsolation())
        {
            if (!TryGetActorCompanyCode(out var actorCompanyCode))
            {
                return await ForbidWithSecurityLogAsync(
                    "MissingCompanyClaim",
                    new Dictionary<string, object?>
                    {
                        ["attemptedAction"] = "Users.Read"
                    },
                    cancellationToken);
            }

            query = query.Where(user => user.CompanyCode == actorCompanyCode);
        }

        var users = await query
            .OrderBy(user => user.UserName)
            .ToListAsync(cancellationToken);
        var companyNameMap = await ResolveCompanyNamesAsync(users, cancellationToken);

        await WriteAuditLogAsync(
            ResolveActorUserId(),
            AuditActionTypes.ViewUsers,
            new Dictionary<string, object?>
            {
                ["resultCount"] = users.Count
            },
            cancellationToken);

        return Ok(users.Select(user => MapToDto(user, companyNameMap)).ToList());
    }

    [HttpGet("{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.UsersRead)]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetUser(Guid userId, CancellationToken cancellationToken)
    {
        var canBypassTenantIsolation = CanBypassTenantIsolation();
        var user = await _dbContext.Users
            .AsNoTracking()
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

        if (!canBypassTenantIsolation)
        {
            if (!TryGetActorCompanyCode(out var actorCompanyCode))
            {
                return await ForbidWithSecurityLogAsync(
                    "MissingCompanyClaim",
                    new Dictionary<string, object?>
                    {
                        ["attemptedAction"] = "Users.Read",
                        ["targetUserId"] = userId
                    },
                    cancellationToken);
            }

            if (user.CompanyCode != actorCompanyCode)
            {
                return await ForbidWithSecurityLogAsync(
                    "CrossTenantReadDenied",
                    new Dictionary<string, object?>
                    {
                        ["attemptedAction"] = "Users.Read",
                        ["targetUserId"] = userId,
                        ["actorCompanyCode"] = actorCompanyCode,
                        ["targetCompanyCode"] = user.CompanyCode
                    },
                    cancellationToken);
            }
        }

        var companyName = await ResolveCompanyNameAsync(user.CompanyCode, cancellationToken);

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

        return Ok(MapToDto(user, companyName));
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.UsersManage)]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var canBypassTenantIsolation = CanBypassTenantIsolation();

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (canBypassTenantIsolation && request.CompanyCode == Guid.Empty)
        {
            ModelState.AddModelError(nameof(request.CompanyCode), "CompanyCode is required.");
            return ValidationProblem(ModelState);
        }

        Guid targetCompanyCode;
        if (canBypassTenantIsolation)
        {
            targetCompanyCode = request.CompanyCode;
        }
        else
        {
            if (!TryGetActorCompanyCode(out var actorCompanyCode))
            {
                return await ForbidWithSecurityLogAsync(
                    "MissingCompanyClaim",
                    new Dictionary<string, object?>
                    {
                        ["attemptedAction"] = "Users.Manage"
                    },
                    cancellationToken);
            }

            if (request.CompanyCode != Guid.Empty && request.CompanyCode != actorCompanyCode)
            {
                return await ForbidWithSecurityLogAsync(
                    "CrossTenantCreateDenied",
                    new Dictionary<string, object?>
                    {
                        ["attemptedAction"] = "Users.Manage",
                        ["requestedCompanyCode"] = request.CompanyCode,
                        ["actorCompanyCode"] = actorCompanyCode
                    },
                    cancellationToken);
            }

            targetCompanyCode = actorCompanyCode;
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

        var resolvedCompanyName = await ResolveCompanyNameAsync(targetCompanyCode, cancellationToken);
        var user = new UserAccount(
            id: Guid.NewGuid(),
            userName: normalizedUserName,
            displayName: normalizedDisplayName,
            passwordHash: _passwordHasher.Hash(request.Password),
            roles: roles,
            isActive: request.IsActive);
        user.SetCompany(targetCompanyCode, resolvedCompanyName ?? request.CompanyName?.Trim(), request.IsCompanyActive);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditLogAsync(
            ResolveActorUserId(),
            AuditActionTypes.CreateUser,
            new Dictionary<string, object?>
            {
                ["targetUserId"] = user.Id,
                ["targetUserName"] = user.UserName,
                ["companyCode"] = user.CompanyCode,
                ["status"] = "Created"
            },
            cancellationToken);

        var response = MapToDto(user, resolvedCompanyName);
        return CreatedAtAction(nameof(GetUser), new { userId = response.UserId }, response);
    }

    [HttpPut("{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.UsersManage)]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> UpdateUser(
        Guid userId,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var canBypassTenantIsolation = CanBypassTenantIsolation();

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (canBypassTenantIsolation && request.CompanyCode == Guid.Empty)
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

        Guid targetCompanyCode;
        if (canBypassTenantIsolation)
        {
            targetCompanyCode = request.CompanyCode;
        }
        else
        {
            if (!TryGetActorCompanyCode(out var actorCompanyCode))
            {
                return await ForbidWithSecurityLogAsync(
                    "MissingCompanyClaim",
                    new Dictionary<string, object?>
                    {
                        ["attemptedAction"] = "Users.Manage",
                        ["targetUserId"] = userId
                    },
                    cancellationToken);
            }

            if (user.CompanyCode != actorCompanyCode)
            {
                return await ForbidWithSecurityLogAsync(
                    "CrossTenantUpdateDenied",
                    new Dictionary<string, object?>
                    {
                        ["attemptedAction"] = "Users.Manage",
                        ["targetUserId"] = userId,
                        ["actorCompanyCode"] = actorCompanyCode,
                        ["targetCompanyCode"] = user.CompanyCode
                    },
                    cancellationToken);
            }

            if (request.CompanyCode != Guid.Empty && request.CompanyCode != actorCompanyCode)
            {
                return await ForbidWithSecurityLogAsync(
                    "CrossTenantCompanyAssignmentDenied",
                    new Dictionary<string, object?>
                    {
                        ["attemptedAction"] = "Users.Manage",
                        ["targetUserId"] = userId,
                        ["requestedCompanyCode"] = request.CompanyCode,
                        ["actorCompanyCode"] = actorCompanyCode
                    },
                    cancellationToken);
            }

            targetCompanyCode = actorCompanyCode;
        }

        user.UpdateProfile(request.DisplayName.Trim(), roles, request.IsActive);
        var passwordChanged = false;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.SetPasswordHash(_passwordHasher.Hash(request.Password));
            passwordChanged = true;
        }

        var resolvedCompanyName = await ResolveCompanyNameAsync(targetCompanyCode, cancellationToken);
        user.SetCompany(targetCompanyCode, resolvedCompanyName ?? request.CompanyName?.Trim(), request.IsCompanyActive);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditLogAsync(
            ResolveActorUserId(),
            AuditActionTypes.UpdateUser,
            new Dictionary<string, object?>
            {
                ["targetUserId"] = user.Id,
                ["targetUserName"] = user.UserName,
                ["companyCode"] = user.CompanyCode,
                ["passwordChanged"] = passwordChanged,
                ["status"] = "Updated"
            },
            cancellationToken);

        return Ok(MapToDto(user, resolvedCompanyName));
    }

    private async Task<IReadOnlyDictionary<Guid, string?>> ResolveCompanyNamesAsync(
        IReadOnlyCollection<UserAccount> users,
        CancellationToken cancellationToken)
    {
        var companyCodes = users
            .Select(user => user.CompanyCode)
            .Where(companyCode => companyCode != Guid.Empty)
            .Distinct()
            .ToArray();

        return await _companyDirectoryService.GetNamesByCodesAsync(companyCodes, cancellationToken);
    }

    private async Task<string?> ResolveCompanyNameAsync(
        Guid companyCode,
        CancellationToken cancellationToken)
    {
        if (companyCode == Guid.Empty)
        {
            return null;
        }

        var entry = await _companyDirectoryService.FindByCodeAsync(companyCode, cancellationToken);
        return entry?.CompanyName;
    }

    private static UserDto MapToDto(
        UserAccount user,
        IReadOnlyDictionary<Guid, string?> companyNameMap)
    {
        companyNameMap.TryGetValue(user.CompanyCode, out var companyName);
        return MapToDto(user, companyName);
    }

    private static UserDto MapToDto(UserAccount user, string? companyName)
    {
        return new UserDto
        {
            UserId = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            IsActive = user.IsActive,
            CompanyCode = user.CompanyCode,
            CompanyName = companyName,
            IsCompanyActive = true,
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

    private bool CanBypassTenantIsolation()
    {
        return TenantContextResolver.CanBypassTenantIsolation(User);
    }

    private bool TryGetActorCompanyCode(out Guid companyCode)
    {
        return TenantContextResolver.TryGetCompanyCode(User, out companyCode);
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

    private async Task<ActionResult> ForbidWithSecurityLogAsync(
        string reason,
        IDictionary<string, object?> customValues,
        CancellationToken cancellationToken)
    {
        await _userActionLogService.LogAsync(
            ResolveActorUserId(),
            AuditActionTypes.SecurityDenied,
            RequestAuditMetadataFactory.Create(HttpContext, new Dictionary<string, object?>(customValues)
            {
                ["reason"] = reason
            }),
            cancellationToken);

        return Forbid();
    }
}
