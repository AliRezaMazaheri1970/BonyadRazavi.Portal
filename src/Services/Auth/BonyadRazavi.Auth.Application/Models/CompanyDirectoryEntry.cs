namespace BonyadRazavi.Auth.Application.Models;

public sealed record CompanyDirectoryEntry(
    Guid CompanyCode,
    string? CompanyName);
