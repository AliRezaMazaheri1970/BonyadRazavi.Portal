using System.Reflection;
using System.Security.Claims;
using BonyadRazavi.Auth.Api.Controllers;
using BonyadRazavi.Auth.Api.Security;
using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Models;
using BonyadRazavi.Auth.Domain.Entities;
using BonyadRazavi.Auth.Infrastructure.Persistence;
using BonyadRazavi.Shared.Contracts.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BonyadRazavi.Auth.Application.Tests;

public sealed class UsersControllerSecurityTests
{
    private static readonly Guid CompanyA = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");
    private static readonly Guid CompanyB = Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB");

    [Fact]
    public async Task GetUser_ReturnsForbid_WhenActorReadsOtherCompanyUser()
    {
        await using var dbContext = CreateDbContext();

        var foreignUser = CreateUser("foreign-user", "Foreign User", PortalRoles.User, CompanyB);
        dbContext.Users.Add(foreignUser);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, PortalRoles.CompanyAdmin, CompanyA);

        var result = await controller.GetUser(foreignUser.Id, CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task CreateUser_ReturnsForbid_WhenActorSetsDifferentCompanyCode()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, PortalRoles.CompanyAdmin, CompanyA);

        var request = new CreateUserRequest
        {
            UserName = "tenant-admin-created",
            DisplayName = "Tenant Admin Created",
            Password = "StrongPass@123",
            Roles = [PortalRoles.User],
            CompanyCode = CompanyB,
            CompanyName = "Company B",
            IsActive = true,
            IsCompanyActive = true
        };

        var result = await controller.CreateUser(request, CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
        Assert.False(await dbContext.Users.AnyAsync(user => user.UserName == request.UserName));
    }

    [Fact]
    public async Task UpdateUser_AllowsAdminToSetEmptyCompanyCode()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser("company-a-user", "Company A User", PortalRoles.User, CompanyA);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, PortalRoles.Admin, CompanyA);
        var request = new UpdateUserRequest
        {
            DisplayName = "Updated User",
            Password = null,
            Roles = [PortalRoles.User],
            CompanyCode = Guid.Empty,
            CompanyName = null,
            IsActive = true,
            IsCompanyActive = true
        };

        var result = await controller.UpdateUser(user.Id, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<UserDto>(okResult.Value);
        Assert.Equal(Guid.Empty, payload.CompanyCode);
    }

    [Fact]
    public async Task GetUsers_ReturnsOnlyActorCompanyUsers_WhenActorIsCompanyAdmin()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.AddRange(
            CreateUser("company-a-user", "Company A User", PortalRoles.User, CompanyA),
            CreateUser("company-b-user", "Company B User", PortalRoles.User, CompanyB));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, PortalRoles.CompanyAdmin, CompanyA);

        var result = await controller.GetUsers(cancellationToken: CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var usersPage = Assert.IsType<PagedUsersResponse>(okResult.Value);
        var users = usersPage.Items;

        Assert.NotEmpty(users);
        Assert.All(users, user => Assert.Equal(CompanyA, user.CompanyCode));
        Assert.Equal(users.Count, usersPage.TotalCount);
    }

    [Fact]
    public async Task GetUsers_ReturnsAllUsers_WhenActorIsAdmin()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.AddRange(
            CreateUser("company-a-user", "Company A User", PortalRoles.User, CompanyA),
            CreateUser("company-b-user", "Company B User", PortalRoles.User, CompanyB));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, PortalRoles.Admin, CompanyA);

        var result = await controller.GetUsers(cancellationToken: CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var usersPage = Assert.IsType<PagedUsersResponse>(okResult.Value);
        var users = usersPage.Items;

        Assert.Equal(2, users.Count);
        Assert.Equal(2, usersPage.TotalCount);
    }

    [Fact]
    public void AuthorizationAttributes_AreConfiguredForPortalPolicies()
    {
        var meMethod = typeof(AuthController).GetMethod(nameof(AuthController.Me));
        Assert.NotNull(meMethod);
        var meAuthorize = meMethod!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(meAuthorize);
        Assert.Equal(AuthorizationPolicies.PortalAccess, meAuthorize!.Policy);

        var usersControllerAuthorize = typeof(UsersController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(usersControllerAuthorize);
        Assert.Equal(AuthorizationPolicies.PortalAccess, usersControllerAuthorize!.Policy);
    }

    private static UsersController CreateController(AuthDbContext dbContext, string role, Guid companyCode)
    {
        var controller = new UsersController(
            dbContext,
            new StubPasswordHasher(),
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

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string rawPassword) => $"hash::{rawPassword}";

        public bool Verify(string hash, string rawPassword) => hash == $"hash::{rawPassword}";
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
