using System.Text.Json;
using System.Text.Json.Serialization;
using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public sealed class DbUserActionLogService : IUserActionLogService
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AuthDbContext _dbContext;
    private readonly ILogger<DbUserActionLogService> _logger;

    public DbUserActionLogService(
        AuthDbContext dbContext,
        ILogger<DbUserActionLogService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LogAsync(
        Guid? userId,
        string actionType,
        object metadata,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actionType))
        {
            throw new ArgumentException("ActionType is required.", nameof(actionType));
        }

        var metadataJson = SerializeMetadata(metadata);

        try
        {
            _dbContext.UserActionLogs.Add(new UserActionLog(userId, actionType, metadataJson, DateTime.UtcNow));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Audit log write failed. ActionType: {ActionType}, UserId: {UserId}",
                actionType,
                userId);
        }
    }

    private static string SerializeMetadata(object metadata)
    {
        var safeMetadata = metadata ?? new { };
        var json = JsonSerializer.Serialize(safeMetadata, MetadataJsonOptions);
        return string.IsNullOrWhiteSpace(json) ? "{}" : json;
    }
}
