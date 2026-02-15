using System.Net.Http.Json;
using BonyadRazavi.Shared.Contracts.Auth;

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

        return LoginApiResult.Failed("ورود ناموفق بود. لطفا اطلاعات را بررسی کنید.", (int)response.StatusCode);
    }
}
