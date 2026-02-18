using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Models;
using BonyadRazavi.Auth.Application.Services;
using BonyadRazavi.Auth.Domain.Entities;
using BonyadRazavi.Shared.Contracts.Auth;

namespace BonyadRazavi.Auth.Application.Tests;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task AuthenticateAsync_ReturnsSuccess_WhenCredentialsAreValid()
    {
        var user = new UserAccount(
            id: Guid.NewGuid(),
            userName: "admin",
            displayName: "Admin User",
            passwordHash: "hashed-password",
            roles: ["Admin"]);
        user.SetCompany(Guid.NewGuid(), "Test Company", isActive: true);

        var service = new AuthenticationService(
            new StubUserRepository(user),
            new StubCompanyDirectoryService(),
            new StubPasswordHasher(isValid: true));

        var result = await service.AuthenticateAsync(new LoginRequest
        {
            UserName = "admin",
            Password = "Razavi@1404"
        });

        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        Assert.Equal("admin", result.User!.UserName);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsFailure_WhenPasswordIsInvalid()
    {
        var user = new UserAccount(
            id: Guid.NewGuid(),
            userName: "admin",
            displayName: "Admin User",
            passwordHash: "hashed-password",
            roles: ["Admin"]);
        user.SetCompany(Guid.NewGuid(), "Test Company", isActive: true);

        var service = new AuthenticationService(
            new StubUserRepository(user),
            new StubCompanyDirectoryService(),
            new StubPasswordHasher(isValid: false));

        var result = await service.AuthenticateAsync(new LoginRequest
        {
            UserName = "admin",
            Password = "wrong-password"
        });

        Assert.False(result.Succeeded);
        Assert.Null(result.User);
    }

    private sealed class StubUserRepository : IUserRepository
    {
        private readonly UserAccount? _user;

        public StubUserRepository(UserAccount? user)
        {
            _user = user;
        }

        public Task<UserAccount?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_user);
        }

        public Task<UserAccount?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_user);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        private readonly bool _isValid;

        public StubPasswordHasher(bool isValid)
        {
            _isValid = isValid;
        }

        public string Hash(string rawPassword) => rawPassword;

        public bool Verify(string hash, string rawPassword) => _isValid;
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
                new CompanyDirectoryEntry(companyCode, "Test Company"));
        }

        public Task<IReadOnlyDictionary<Guid, string?>> GetNamesByCodesAsync(
            IReadOnlyCollection<Guid> companyCodes,
            CancellationToken cancellationToken = default)
        {
            var map = companyCodes
                .Distinct()
                .ToDictionary(code => code, _ => (string?)"Test Company");

            return Task.FromResult<IReadOnlyDictionary<Guid, string?>>(map);
        }
    }
}
