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
}
