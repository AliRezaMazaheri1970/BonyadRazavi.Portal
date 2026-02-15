using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Domain.Entities;

namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly Dictionary<string, UserAccount> _users;

    public InMemoryUserRepository(IPasswordHasher passwordHasher)
    {
        _users = new Dictionary<string, UserAccount>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin"] = new UserAccount(
                id: Guid.Parse("6CA89A09-0F56-44E3-A819-BE9B5D5DA5E0"),
                userName: "admin",
                displayName: "مدیر سامانه",
                passwordHash: passwordHasher.Hash("Razavi@1404"),
                roles: ["Admin", "Operator"])
        };
    }

    public Task<UserAccount?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        _users.TryGetValue(userName, out var user);
        return Task.FromResult(user);
    }
}
