using BonyadRazavi.Auth.Api.Audit;
using BonyadRazavi.Auth.Api.Security;
using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Constants;
using BonyadRazavi.Auth.Domain.Entities;
using BonyadRazavi.Auth.Infrastructure.Persistence;
using BonyadRazavi.Shared.Contracts.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BonyadRazavi.Auth.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Policy = AuthorizationPolicies.AuditRead)]
public sealed class AuditController : ControllerBase
{
    private readonly AuthDbContext _dbContext;
    private readonly IUserActionLogService _userActionLogService;

    public AuditController(
        AuthDbContext dbContext,
        IUserActionLogService userActionLogService)
    {
        _dbContext = dbContext;
        _userActionLogService = userActionLogService;
    }

    [HttpGet("actions")]
    [ProducesResponseType<AuditLogPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuditLogPageResponse>> GetActions(
        [FromQuery] AuditLogQueryRequest query,
        CancellationToken cancellationToken)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var canBypassTenantIsolation = TenantContextResolver.CanBypassTenantIsolation(User);

        IQueryable<UserActionLog> logQuery = _dbContext.UserActionLogs
            .AsNoTracking()
            .Include(log => log.User);

        if (query.UserId.HasValue)
        {
            logQuery = logQuery.Where(log => log.UserId == query.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.ActionType))
        {
            var actionType = query.ActionType.Trim();
            logQuery = logQuery.Where(log => log.ActionType == actionType);
        }

        if (query.FromUtc.HasValue)
        {
            logQuery = logQuery.Where(log => log.ActionDateUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            logQuery = logQuery.Where(log => log.ActionDateUtc <= query.ToUtc.Value);
        }

        if (!canBypassTenantIsolation)
        {
            if (!TenantContextResolver.TryGetCompanyCode(User, out var actorCompanyCode))
            {
                return await ForbidWithSecurityLogAsync(
                    "MissingCompanyClaim",
                    new Dictionary<string, object?>
                    {
                        ["attemptedAction"] = "Audit.Read"
                    },
                    cancellationToken);
            }

            logQuery = logQuery.Where(log =>
                log.UserId.HasValue &&
                _dbContext.Users.Any(user =>
                    user.Id == log.UserId.Value &&
                    user.CompanyCode == actorCompanyCode));
        }

        var totalCount = await logQuery.CountAsync(cancellationToken);
        var items = await logQuery
            .OrderByDescending(log => log.ActionDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new AuditLogDto
            {
                Id = log.Id,
                UserId = log.UserId,
                UserName = log.User != null ? log.User.UserName : null,
                ActionDateUtc = log.ActionDateUtc,
                ActionType = log.ActionType,
                Metadata = log.Metadata
            })
            .ToListAsync(cancellationToken);

        await _userActionLogService.LogAsync(
            RequestAuditMetadataFactory.ResolveAuthenticatedUserId(User),
            AuditActionTypes.ViewAuditLogs,
            RequestAuditMetadataFactory.Create(HttpContext, new Dictionary<string, object?>
            {
                ["requestedPage"] = page,
                ["requestedPageSize"] = pageSize,
                ["resultCount"] = items.Count
            }),
            cancellationToken);

        return Ok(new AuditLogPageResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
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
}
