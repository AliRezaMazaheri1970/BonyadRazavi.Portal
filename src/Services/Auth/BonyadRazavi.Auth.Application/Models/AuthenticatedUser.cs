namespace BonyadRazavi.Auth.Application.Models;

public sealed record AuthenticatedUser(
    Guid Id,
    string UserName,
    string DisplayName,
    IReadOnlyCollection<string> Roles,
    Guid CompanyCode,
    string? CompanyName);
