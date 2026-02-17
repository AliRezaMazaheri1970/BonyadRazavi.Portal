using System.Net.Http.Json;
using BonyadRazavi.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;

namespace BonyadRazavi.WebApp.Services;

public sealed class AuthApiClient
{
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
                return LoginApiResult.Failed("پاسخ سرویس احراز هویت معتبر نیست.");
            }

            return LoginApiResult.Succeeded(payload);
        }

        ProblemDetails? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        }
        catch
        {
            // Ignore invalid problem-details payload and use default text.
        }

        var errorText = !string.IsNullOrWhiteSpace(problem?.Detail)
            ? problem.Detail
            : "ورود ناموفق بود. لطفا اطلاعات را بررسی کنید.";

        return LoginApiResult.Failed($"{errorText} (HTTP {(int)response.StatusCode})", (int)response.StatusCode);
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
                return LoginApiResult.Failed("پاسخ سرویس تمدید نشست معتبر نیست.");
            }

            return LoginApiResult.Succeeded(payload);
        }

        ProblemDetails? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        }
        catch
        {
            // Ignore invalid problem-details payload and use default text.
        }

        var errorText = !string.IsNullOrWhiteSpace(problem?.Detail)
            ? problem.Detail
            : "تمدید نشست ناموفق بود.";

        return LoginApiResult.Failed($"{errorText} (HTTP {(int)response.StatusCode})", (int)response.StatusCode);
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
}
