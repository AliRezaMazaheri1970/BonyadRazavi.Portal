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

        var (currentPassword, nextPassword) = await ResolveAdminPasswordPairAsync(client);
        var initialLogin = await LoginAsync(client, "admin", currentPassword);
        Assert.NotNull(initialLogin);

        var changeRequest = new ChangePasswordRequest
        {
            CurrentPassword = currentPassword,
            NewPassword = nextPassword,
            ConfirmNewPassword = nextPassword
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

        var loginWithOldPassword = await LoginRawAsync(client, "admin", currentPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, loginWithOldPassword.StatusCode);

        var loginWithNewPassword = await LoginAsync(client, "admin", nextPassword);
        Assert.NotNull(loginWithNewPassword);
    }

    private static async Task<(string CurrentPassword, string NextPassword)> ResolveAdminPasswordPairAsync(HttpClient client)
    {
        const string firstCandidate = "Razavi@1404";
        const string secondCandidate = "Razavi@1405!";

        if (await LoginAsync(client, "admin", firstCandidate) is not null)
        {
            return (firstCandidate, secondCandidate);
        }

        if (await LoginAsync(client, "admin", secondCandidate) is not null)
        {
            return (secondCandidate, firstCandidate);
        }

        throw new InvalidOperationException("Unable to login with known admin credentials.");
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
