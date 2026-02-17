namespace BonyadRazavi.Shared.Contracts.Audit;

public sealed class AuditLogQueryRequest
{
    public Guid? UserId { get; set; }
    public string? ActionType { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
