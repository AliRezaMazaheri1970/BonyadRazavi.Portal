using System.Net.Http.Json;
using BonyadRazavi.Shared.Contracts.Auth;
using BonyadRazavi.Shared.Contracts.Common;
using Microsoft.AspNetCore.Mvc;

namespace BonyadRazavi.WebApp.Services;

public sealed class AuthApiClient
{
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    private readonly HttpClient _httpClient;

    public AuthApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LoginApiResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/auth/login",
            request,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
            if (payload is null)
            {
                var payloadCorrelationId = ResolveCorrelationId(response, problem: null);
                return BuildFailureResult(
                    "پاسخ سرویس احراز هویت معتبر نیست.",
                    statusCode: (int)response.StatusCode,
                    correlationId: payloadCorrelationId);
            }

            return LoginApiResult.Succeeded(payload);
        }

        var problem = await ReadProblemDetailsAsync(response, cancellationToken);
        var errorText = !string.IsNullOrWhiteSpace(problem?.Detail)
            ? problem.Detail
            : "ورود ناموفق بود. لطفا اطلاعات را بررسی کنید.";
        var correlationId = ResolveCorrelationId(response, problem);
        var validationErrors = ExtractValidationErrors(problem);

        return BuildFailureResult(
            errorText,
            statusCode: (int)response.StatusCode,
            correlationId: correlationId,
            errors: validationErrors);
    }

    public async Task<LoginApiResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = refreshToken },
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
            if (payload is null)
            {
                var payloadCorrelationId = ResolveCorrelationId(response, problem: null);
                return BuildFailureResult(
                    "پاسخ سرویس تمدید نشست معتبر نیست.",
                    statusCode: (int)response.StatusCode,
                    correlationId: payloadCorrelationId);
            }

            return LoginApiResult.Succeeded(payload);
        }

        var problem = await ReadProblemDetailsAsync(response, cancellationToken);
        var errorText = !string.IsNullOrWhiteSpace(problem?.Detail)
            ? problem.Detail
            : "تمدید نشست ناموفق بود.";
        var correlationId = ResolveCorrelationId(response, problem);
        var validationErrors = ExtractValidationErrors(problem);

        return BuildFailureResult(
            errorText,
            statusCode: (int)response.StatusCode,
            correlationId: correlationId,
            errors: validationErrors);
    }

    public async Task<bool> RevokeAsync(
        string refreshToken,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/auth/revoke",
            new RevokeRefreshTokenRequest
            {
                RefreshToken = refreshToken,
                Reason = reason
            },
            cancellationToken);

        return response.IsSuccessStatusCode;
    }

    private static async Task<ProblemDetails?> ReadProblemDetailsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        }
        catch
        {
            // Ignore invalid payload and fallback to default message.
            return null;
        }
    }

    private static IReadOnlyCollection<string> ExtractValidationErrors(ProblemDetails? problem)
    {
        if (problem is not ValidationProblemDetails validationProblem)
        {
            return [];
        }

        return validationProblem.Errors.Values
            .SelectMany(messages => messages)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string? ResolveCorrelationId(
        HttpResponseMessage response,
        ProblemDetails? problem)
    {
        if (response.Headers.TryGetValues(CorrelationIdHeaderName, out var values))
        {
            var headerValue = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                return headerValue;
            }
        }

        if (problem?.Extensions.TryGetValue("correlationId", out var extensionValue) == true)
        {
            var correlationId = extensionValue?.ToString();
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                return correlationId;
            }
        }

        return null;
    }

    private static LoginApiResult BuildFailureResult(
        string message,
        int statusCode,
        string? correlationId,
        IReadOnlyCollection<string>? errors = null)
    {
        var apiResult = ApiResult.Fail(message, errors, correlationId);
        var finalMessage = apiResult.Message;
        if (!string.IsNullOrWhiteSpace(apiResult.CorrelationId))
        {
            finalMessage = $"{finalMessage} (کد پیگیری: {apiResult.CorrelationId})";
        }

        return LoginApiResult.Failed(
            $"{finalMessage} (HTTP {statusCode})",
            statusCode,
            apiResult.CorrelationId);
    }
}
