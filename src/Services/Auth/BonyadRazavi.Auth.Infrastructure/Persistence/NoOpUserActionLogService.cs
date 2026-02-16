using BonyadRazavi.Auth.Application.Abstractions;

namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public sealed class NoOpUserActionLogService : IUserActionLogService
{
    public Task LogAsync(
        Guid? userId,
        string actionType,
        object metadata,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
