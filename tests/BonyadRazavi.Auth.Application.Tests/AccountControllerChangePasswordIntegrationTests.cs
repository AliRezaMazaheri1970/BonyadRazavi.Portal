using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BonyadRazavi.Shared.Contracts.Account;
using BonyadRazavi.Shared.Contracts.Auth;
using BonyadRazavi.Shared.Contracts.Common;

namespace BonyadRazavi.Auth.Application.Tests;

public sealed class AccountControllerChangePasswordIntegrationTests
{
    [Fact]
    public async Task ChangePassword_WithValidCurrentPassword_UpdatesLoginCredential()
    {
        await using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var initialLogin = await LoginAsync(client, "admin", "Razavi@1404");
        Assert.NotNull(initialLogin);

        var changeRequest = new ChangePasswordRequest
        {
            CurrentPassword = "Razavi@1404",
            NewPassword = "Razavi@1405!",
            ConfirmNewPassword = "Razavi@1405!"
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/account/change-password")
        {
            Content = JsonContent.Create(changeRequest)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", initialLogin!.AccessToken);

        var response = await client.SendAsync(httpRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResult>();
        Assert.NotNull(payload);
        Assert.True(payload!.Success);

        var loginWithOldPassword = await LoginRawAsync(client, "admin", "Razavi@1404");
        Assert.Equal(HttpStatusCode.Unauthorized, loginWithOldPassword.StatusCode);

        var loginWithNewPassword = await LoginAsync(client, "admin", "Razavi@1405!");
        Assert.NotNull(loginWithNewPassword);
    }

    private static async Task<LoginResponse?> LoginAsync(HttpClient client, string userName, string password)
    {
        var response = await LoginRawAsync(client, userName, password);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<LoginResponse>();
    }

    private static Task<HttpResponseMessage> LoginRawAsync(HttpClient client, string userName, string password)
    {
        return client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                UserName = userName,
                Password = password
            });
    }
}
