using System.Net.Http.Headers;
using System.Net.Http.Json;
using BonyadRazavi.Shared.Contracts.Users;
using Microsoft.AspNetCore.Mvc;

namespace BonyadRazavi.WebApp.Services;

public sealed class UsersApiClient
{
    private readonly HttpClient _httpClient;

    public UsersApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UsersApiResult> GetUsersAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var users = await response.Content.ReadFromJsonAsync<List<UserDto>>(cancellationToken);
            return UsersApiResult.Succeeded(users ?? []);
        }

        var problem = await ReadProblemDetailsAsync(response, cancellationToken);
        var message = !string.IsNullOrWhiteSpace(problem?.Detail)
            ? problem.Detail
            : "دریافت لیست کاربران ناموفق بود.";

        return UsersApiResult.Failed(message, (int)response.StatusCode);
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
            return null;
        }
    }
}
