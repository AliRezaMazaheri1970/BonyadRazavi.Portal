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
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/users/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("X-Access-Token", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var users = await response.Content.ReadFromJsonAsync<List<UserDto>>(cancellationToken);
            return UsersApiResult.Succeeded(users ?? []);
        }

        var problem = await ReadProblemDetailsAsync(response, cancellationToken);
        var message = BuildFailureMessage(response, problem);

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

    private static string BuildFailureMessage(
        HttpResponseMessage response,
        ProblemDetails? problem)
    {
        var authChallenge = response.Headers.WwwAuthenticate.Count > 0
            ? string.Join(" | ", response.Headers.WwwAuthenticate.Select(header => header.ToString()))
            : null;

        if (!string.IsNullOrWhiteSpace(problem?.Detail))
        {
            if (!string.IsNullOrWhiteSpace(authChallenge))
            {
                return $"{problem.Detail} ({authChallenge})";
            }

            return problem.Detail;
        }

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                !string.IsNullOrWhiteSpace(authChallenge)
                    ? $"نشست کاربری معتبر نیست. لطفا دوباره وارد شوید. ({authChallenge})"
                    : "نشست کاربری معتبر نیست. لطفا دوباره وارد شوید.",
            System.Net.HttpStatusCode.Forbidden =>
                "شما مجوز مشاهده لیست کاربران را ندارید.",
            System.Net.HttpStatusCode.NotFound =>
                "مسیر سرویس مدیریت کاربران در Gateway پیدا نشد.",
            _ => $"دریافت لیست کاربران ناموفق بود. (HTTP {(int)response.StatusCode})"
        };
    }
}
