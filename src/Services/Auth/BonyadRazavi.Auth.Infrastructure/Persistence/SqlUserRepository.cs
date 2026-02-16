using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public sealed class SqlUserRepository : IUserRepository
{
    private readonly AuthDbContext _dbContext;

    public SqlUserRepository(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserAccount?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .Include(user => user.Company)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.UserName == userName && user.IsActive,
                cancellationToken);
    }
}
