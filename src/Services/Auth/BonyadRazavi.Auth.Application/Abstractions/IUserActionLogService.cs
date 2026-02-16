namespace BonyadRazavi.Auth.Application.Abstractions;

public interface IUserActionLogService
{
    Task LogAsync(
        Guid? userId,
        string actionType,
        object metadata,
        CancellationToken cancellationToken = default);
}
