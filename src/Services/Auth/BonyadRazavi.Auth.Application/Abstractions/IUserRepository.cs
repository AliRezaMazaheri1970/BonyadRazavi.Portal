using BonyadRazavi.Auth.Domain.Entities;

namespace BonyadRazavi.Auth.Application.Abstractions;

public interface IUserRepository
{
    Task<UserAccount?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<UserAccount?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
