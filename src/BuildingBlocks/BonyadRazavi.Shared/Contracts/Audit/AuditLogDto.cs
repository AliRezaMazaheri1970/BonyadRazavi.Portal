namespace BonyadRazavi.Shared.Contracts.Audit;

public sealed class AuditLogDto
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime ActionDateUtc { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Metadata { get; set; } = "{}";
}
