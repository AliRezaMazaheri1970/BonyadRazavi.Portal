namespace BonyadRazavi.Auth.Application.Models;

public sealed record RefreshTokenIssueResult(string Token, DateTime ExpiresAtUtc);
