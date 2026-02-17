using System.Reflection;
using System.Security.Claims;
using BonyadRazavi.Auth.Api.Controllers;
using BonyadRazavi.Auth.Api.Security;
using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Models;
using BonyadRazavi.Auth.Domain.Entities;
using BonyadRazavi.Auth.Infrastructure.Persistence;
using BonyadRazavi.Shared.Contracts.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BonyadRazavi.Auth.Application.Tests;

public sealed class CompaniesControllerSecurityTests
{
    private static readonly Guid CompanyA = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");
    private static readonly Guid CompanyB = Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB");

    [Fact]
    public async Task GetCompanies_ReturnsOnlyActorCompany_WhenActorIsCompanyAdmin()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.AddRange(
            CreateUser("company-a-user", "Company A User", PortalRoles.User, CompanyA),
            CreateUser("company-b-user", "Company B User", PortalRoles.User, CompanyB));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, PortalRoles.CompanyAdmin, CompanyA);

        var result = await controller.GetCompanies(false, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var companies = Assert.IsAssignableFrom<IReadOnlyCollection<CompanyDto>>(okResult.Value);

        var only = Assert.Single(companies);
        Assert.Equal(CompanyA, only.CompanyCode);
    }

    [Fact]
    public async Task GetCompany_ReturnsForbid_WhenActorReadsOtherCompany()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.AddRange(
            CreateUser("company-a-user", "Company A User", PortalRoles.User, CompanyA),
            CreateUser("company-b-user", "Company B User", PortalRoles.User, CompanyB));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, PortalRoles.CompanyAdmin, CompanyA);

        var result = await controller.GetCompany(CompanyB, CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetCompanies_ReturnsAllCompanies_WhenActorIsAdmin()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.AddRange(
            CreateUser("company-a-user-1", "Company A User 1", PortalRoles.User, CompanyA),
            CreateUser("company-a-user-2", "Company A User 2", PortalRoles.User, CompanyA),
            CreateUser("company-b-user", "Company B User", PortalRoles.User, CompanyB));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, PortalRoles.Admin, CompanyA);

        var result = await controller.GetCompanies(false, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var companies = Assert.IsAssignableFrom<IReadOnlyCollection<CompanyDto>>(okResult.Value);

        Assert.Equal(2, companies.Count);
    }

    [Fact]
    public async Task UpdateCompany_ReturnsForbid_WhenActorUpdatesOtherCompany()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.AddRange(
            CreateUser("company-a-user", "Company A User", PortalRoles.User, CompanyA),
            CreateUser("company-b-user", "Company B User", PortalRoles.User, CompanyB));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, PortalRoles.CompanyAdmin, CompanyA);
        var request = new UpdateCompanyRequest
        {
            CompanyName = "Company B Updated",
            IsActive = false
        };

        var result = await controller.UpdateCompany(CompanyB, request, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateCompany_ReturnsConflict_WhenSourceIsReadOnly()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.AddRange(
            CreateUser("company-a-user-1", "Company A User 1", PortalRoles.User, CompanyA),
            CreateUser("company-a-user-2", "Company A User 2", PortalRoles.Manager, CompanyA));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, PortalRoles.Admin, CompanyA);
        var request = new UpdateCompanyRequest
        {
            CompanyName = "Updated A",
            IsActive = false
        };

        var result = await controller.UpdateCompany(CompanyA, request, CancellationToken.None);
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public void AuthorizationAttributes_AreConfiguredForCompaniesPolicies()
    {
        var classAuthorize = typeof(CompaniesController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(classAuthorize);
        Assert.Equal(AuthorizationPolicies.CompaniesRead, classAuthorize!.Policy);

        var updateMethod = typeof(CompaniesController).GetMethod(nameof(CompaniesController.UpdateCompany));
        Assert.NotNull(updateMethod);
        var updateAuthorize = updateMethod!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(updateAuthorize);
        Assert.Equal(AuthorizationPolicies.CompaniesManage, updateAuthorize!.Policy);
    }

    private static CompaniesController CreateController(AuthDbContext dbContext, string role, Guid companyCode)
    {
        var controller = new CompaniesController(
            dbContext,
            new StubCompanyDirectoryService(),
            new NoOpUserActionLogService());

        var principal = BuildPrincipal(Guid.NewGuid(), $"actor-{role}", role, companyCode);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };

        return controller;
    }

    private static ClaimsPrincipal BuildPrincipal(Guid userId, string userName, string role, Guid companyCode)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, userName),
            new Claim(ClaimTypes.Role, role),
            new Claim("company_code", companyCode.ToString())
        ], "TestAuth");

        return new ClaimsPrincipal(identity);
    }

    private static UserAccount CreateUser(string userName, string displayName, string role, Guid companyCode)
    {
        var user = new UserAccount(
            id: Guid.NewGuid(),
            userName: userName,
            displayName: displayName,
            passwordHash: "hash",
            roles: [role],
            isActive: true);
        user.SetCompany(companyCode, $"Company-{companyCode}", isActive: true);
        return user;
    }

    private static AuthDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
            .Options;

        return new AuthDbContext(options);
    }

    private sealed class StubCompanyDirectoryService : ICompanyDirectoryService
    {
        public Task<CompanyDirectoryEntry?> FindByCodeAsync(
            Guid companyCode,
            CancellationToken cancellationToken = default)
        {
            if (companyCode == Guid.Empty)
            {
                return Task.FromResult<CompanyDirectoryEntry?>(null);
            }

            return Task.FromResult<CompanyDirectoryEntry?>(
                new CompanyDirectoryEntry(companyCode, $"Company-{companyCode}"));
        }

        public Task<IReadOnlyDictionary<Guid, string?>> GetNamesByCodesAsync(
            IReadOnlyCollection<Guid> companyCodes,
            CancellationToken cancellationToken = default)
        {
            var map = companyCodes
                .Distinct()
                .ToDictionary(code => code, code => (string?)$"Company-{code}");
            return Task.FromResult<IReadOnlyDictionary<Guid, string?>>(map);
        }
    }
}
