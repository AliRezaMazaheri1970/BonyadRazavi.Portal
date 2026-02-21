using BonyadRazavi.Auth.Api.Audit;
using BonyadRazavi.Auth.Api.Security;
using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Constants;
using BonyadRazavi.Auth.Infrastructure.Persistence;
using BonyadRazavi.Shared.Contracts.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BonyadRazavi.Auth.Api.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize(Policy = AuthorizationPolicies.CompaniesRead)]
public sealed class CompaniesController : ControllerBase
{
    private readonly AuthDbContext _dbContext;
    private readonly ICompanyDirectoryService _companyDirectoryService;
    private readonly IUserActionLogService _userActionLogService;

    public CompaniesController(
        AuthDbContext dbContext,
        ICompanyDirectoryService companyDirectoryService,
        IUserActionLogService userActionLogService)
    {
        _dbContext = dbContext;
        _companyDirectoryService = companyDirectoryService;
        _userActionLogService = userActionLogService;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<CompanyDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<CompanyDto>>> GetCompanies(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<CompanyUserProjection> query = _dbContext.Users
            .AsNoTracking()
            .Where(user => user.CompanyCode != Guid.Empty)
            .Select(user => new CompanyUserProjection(
                user.CompanyCode,
                user.IsActive,
                user.CreatedAtUtc));

        if (!TenantContextResolver.CanBypassTenantIsolation(User))
        {
            if (!TenantContextResolver.TryGetCompanyCode(User, out var actorCompanyCode))
            {
                return await ForbidWithSecurityLogAsync(
                    "MissingCompanyClaim",
                    new Dictionary<string, object?>
                    {
                        ["attemptedAction"] = "Companies.Read"
                    },
                    cancellationToken);
            }

            query = query.Where(company => company.CompanyCode == actorCompanyCode);
        }

        var companyUsers = await query.ToListAsync(cancellationToken);
        var grouped = companyUsers.GroupBy(company => company.CompanyCode).ToList();
        var companyNames = await _companyDirectoryService.GetNamesByCodesAsync(
            grouped.Select(group => group.Key).ToArray(),
            cancellationToken);

        var companies = grouped
            .Select(group =>
            {
                companyNames.TryGetValue(group.Key, out var companyName);
                return new CompanyDto
                {
                    CompanyCode = group.Key,
                    CompanyName = companyName,
                    IsActive = group.Any(item => item.IsActive),
                    UsersCount = group.Count(),
                    CreatedAtUtc = group.Min(item => item.CreatedAtUtc)
                };
            })
            .Where(company => includeInactive || company.IsActive)
            .OrderBy(company => company.CompanyName ?? string.Empty)
            .ThenBy(company => company.CompanyCode)
            .ToList();

        await _userActionLogService.LogAsync(
            RequestAuditMetadataFactory.ResolveAuthenticatedUserId(User),
            AuditActionTypes.ViewCompany,
            RequestAuditMetadataFactory.Create(HttpContext, new Dictionary<string, object?>
            {
                ["scope"] = "List",
                ["includeInactive"] = includeInactive,
                ["resultCount"] = companies.Count
            }),
            cancellationToken);

        return Ok(companies);
    }

    [HttpGet("directory")]
    [ProducesResponseType<IReadOnlyCollection<CompanyDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<CompanyDto>>> GetCompanyDirectory(
        CancellationToken cancellationToken = default)
    {
        var directoryEntries = await _companyDirectoryService.GetAllAsync(cancellationToken);

        if (!TenantContextResolver.CanBypassTenantIsolation(User))
        {
            if (!TenantContextResolver.TryGetCompanyCode(User, out var actorCompanyCode))
            {
                return await ForbidWithSecurityLogAsync(
                    "MissingCompanyClaim",
                    new Dictionary<string, object?>
                    {
                        ["attemptedAction"] = "Companies.ReadDirectory"
                    },
                    cancellationToken);
            }

            directoryEntries = directoryEntries
                .Where(entry => entry.CompanyCode == actorCompanyCode)
                .ToArray();
        }

        var usageStats = await GetCompanyUsageStatsAsync(cancellationToken);
        var companies = directoryEntries
            .Select(entry =>
            {
                usageStats.TryGetValue(entry.CompanyCode, out var usage);
                return new CompanyDto
                {
                    CompanyCode = entry.CompanyCode,
                    CompanyName = entry.CompanyName,
                    IsActive = usage?.IsActive ?? true,
                    UsersCount = usage?.UsersCount ?? 0,
                    CreatedAtUtc = usage?.CreatedAtUtc ?? DateTime.MinValue
                };
            })
            .OrderBy(company => company.CompanyName ?? string.Empty)
            .ThenBy(company => company.CompanyCode)
            .ToList();

        await _userActionLogService.LogAsync(
            RequestAuditMetadataFactory.ResolveAuthenticatedUserId(User),
            AuditActionTypes.ViewCompany,
            RequestAuditMetadataFactory.Create(HttpContext, new Dictionary<string, object?>
            {
                ["scope"] = "Directory",
                ["resultCount"] = companies.Count
            }),
            cancellationToken);

        return Ok(companies);
    }

    [HttpGet("{companyCode:guid}")]
    [ProducesResponseType<CompanyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyDto>> GetCompany(
        Guid companyCode,
        CancellationToken cancellationToken)
    {
        if (!TenantContextResolver.CanBypassTenantIsolation(User))
        {
            if (!TenantContextResolver.TryGetCompanyCode(User, out var actorCompanyCode))
            {
                return await ForbidWithSecurityLogAsync(
                    "MissingCompanyClaim",
                    new Dictionary<string, object?>
                    {
                        ["attemptedAction"] = "Companies.Read",
                        ["targetCompanyCode"] = companyCode
                    },
                    cancellationToken);
            }

            if (actorCompanyCode != companyCode)
            {
                return await ForbidWithSecurityLogAsync(
                    "CrossTenantCompanyReadDenied",
                    new Dictionary<string, object?>
                    {
                        ["attemptedAction"] = "Companies.Read",
                        ["targetCompanyCode"] = companyCode,
                        ["actorCompanyCode"] = actorCompanyCode
                    },
                    cancellationToken);
            }
        }

        var companyUsers = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.CompanyCode == companyCode)
            .Select(user => new CompanyUserProjection(
                user.CompanyCode,
                user.IsActive,
                user.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        if (companyUsers.Count == 0)
        {
            await _userActionLogService.LogAsync(
                RequestAuditMetadataFactory.ResolveAuthenticatedUserId(User),
                AuditActionTypes.ViewCompany,
                RequestAuditMetadataFactory.Create(HttpContext, new Dictionary<string, object?>
                {
                    ["scope"] = "Single",
                    ["targetCompanyCode"] = companyCode,
                    ["found"] = false
                }),
                cancellationToken);

            return NotFound();
        }

        var companyName = (await _companyDirectoryService.FindByCodeAsync(companyCode, cancellationToken))?.CompanyName;
        var company = new CompanyDto
        {
            CompanyCode = companyCode,
            CompanyName = companyName,
            IsActive = companyUsers.Any(item => item.IsActive),
            UsersCount = companyUsers.Count,
            CreatedAtUtc = companyUsers.Min(item => item.CreatedAtUtc)
        };

        await _userActionLogService.LogAsync(
            RequestAuditMetadataFactory.ResolveAuthenticatedUserId(User),
            AuditActionTypes.ViewCompany,
            RequestAuditMetadataFactory.Create(HttpContext, new Dictionary<string, object?>
            {
                ["scope"] = "Single",
                ["targetCompanyCode"] = companyCode,
                ["found"] = true,
                ["usersCount"] = company.UsersCount
            }),
            cancellationToken);

        return Ok(company);
    }

    [HttpPut("{companyCode:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CompaniesManage)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> UpdateCompany(
        Guid companyCode,
        [FromBody] UpdateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TenantContextResolver.CanBypassTenantIsolation(User))
        {
            if (!TenantContextResolver.TryGetCompanyCode(User, out var actorCompanyCode))
            {
                return await ForbidWithSecurityLogAsync(
                    "MissingCompanyClaim",
                    new Dictionary<string, object?>
                    {
                        ["attemptedAction"] = "Companies.Manage",
                        ["targetCompanyCode"] = companyCode
                    },
                    cancellationToken);
            }

            if (actorCompanyCode != companyCode)
            {
                return await ForbidWithSecurityLogAsync(
                    "CrossTenantCompanyUpdateDenied",
                    new Dictionary<string, object?>
                    {
                        ["attemptedAction"] = "Companies.Manage",
                        ["targetCompanyCode"] = companyCode,
                        ["actorCompanyCode"] = actorCompanyCode
                    },
                    cancellationToken);
            }
        }

        await _userActionLogService.LogAsync(
            RequestAuditMetadataFactory.ResolveAuthenticatedUserId(User),
            AuditActionTypes.UpdateCompany,
            RequestAuditMetadataFactory.Create(HttpContext, new Dictionary<string, object?>
            {
                ["targetCompanyCode"] = companyCode,
                ["status"] = "Rejected",
                ["reason"] = "ExternalSourceReadOnly"
            }),
            cancellationToken);

        return Conflict(new ProblemDetails
        {
            Title = "Read-only company source",
            Detail = "Company metadata is loaded from LaboratoryRASF.dbo.Companies_Base and cannot be modified here.",
            Status = StatusCodes.Status409Conflict
        });
    }

    private async Task<ActionResult> ForbidWithSecurityLogAsync(
        string reason,
        IDictionary<string, object?> customValues,
        CancellationToken cancellationToken)
    {
        await _userActionLogService.LogAsync(
            RequestAuditMetadataFactory.ResolveAuthenticatedUserId(User),
            AuditActionTypes.SecurityDenied,
            RequestAuditMetadataFactory.Create(HttpContext, new Dictionary<string, object?>(customValues)
            {
                ["reason"] = reason
            }),
            cancellationToken);

        return Forbid();
    }

    private async Task<IReadOnlyDictionary<Guid, CompanyUsageProjection>> GetCompanyUsageStatsAsync(
        CancellationToken cancellationToken)
    {
        var stats = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.CompanyCode != Guid.Empty)
            .GroupBy(user => user.CompanyCode)
            .Select(group => new CompanyUsageProjection(
                group.Key,
                group.Any(user => user.IsActive),
                group.Count(),
                group.Min(user => user.CreatedAtUtc)))
            .ToListAsync(cancellationToken);

        return stats.ToDictionary(item => item.CompanyCode);
    }

    private sealed record CompanyUserProjection(
        Guid CompanyCode,
        bool IsActive,
        DateTime CreatedAtUtc);

    private sealed record CompanyUsageProjection(
        Guid CompanyCode,
        bool IsActive,
        int UsersCount,
        DateTime CreatedAtUtc);
}
