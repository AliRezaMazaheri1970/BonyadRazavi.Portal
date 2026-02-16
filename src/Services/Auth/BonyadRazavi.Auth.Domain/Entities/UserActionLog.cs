namespace BonyadRazavi.Auth.Domain.Entities;

public sealed class UserActionLog
{
    private UserActionLog()
    {
    }

    public UserActionLog(
        Guid? userId,
        string actionType,
        string metadata,
        DateTime? actionDateUtc = null)
    {
        UserId = userId;
        ActionType = actionType;
        Metadata = metadata;
        ActionDateUtc = actionDateUtc ?? DateTime.UtcNow;
    }

    public long Id { get; private set; }
    public Guid? UserId { get; private set; }
    public DateTime ActionDateUtc { get; private set; }
    public string ActionType { get; private set; } = string.Empty;
    public string Metadata { get; private set; } = "{}";

    public UserAccount? User { get; private set; }
}
