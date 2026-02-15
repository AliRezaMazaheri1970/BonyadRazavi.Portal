using BonyadRazavi.Auth.Domain.Entities;

namespace BonyadRazavi.Auth.Application.Abstractions;

public interface IUserRepository
{
    Task<UserAccount?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default);
}
