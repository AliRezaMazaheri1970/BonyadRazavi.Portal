using BonyadRazavi.Shared.Contracts.Auth;

namespace BonyadRazavi.WebApp.Services;

public sealed record LoginApiResult(
    bool IsSuccess,
    LoginResponse? Payload,
    string? ErrorMessage,
    int? StatusCode,
    string? CorrelationId)
{
    public static LoginApiResult Succeeded(LoginResponse payload) =>
        new(true, payload, null, null, null);

    public static LoginApiResult Failed(
        string errorMessage,
        int? statusCode = null,
        string? correlationId = null) =>
        new(false, null, errorMessage, statusCode, correlationId);
}
