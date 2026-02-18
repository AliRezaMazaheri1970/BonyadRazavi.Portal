using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BonyadRazavi.Shared.Contracts.Account;
using BonyadRazavi.Shared.Contracts.Common;
using Microsoft.AspNetCore.Mvc;

namespace BonyadRazavi.WebApp.Services;

public sealed class ChangePasswordApiClient
{
    private readonly HttpClient _httpClient;

    public ChangePasswordApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ChangePasswordApiResult> ChangePasswordAsync(
        string accessToken,
        ChangePasswordRequest requestModel,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/account/change-password")
        {
            Content = JsonContent.Create(requestModel)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("X-Access-Token", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var payload = await TryReadApiResultAsync(response, cancellationToken);
            var message = payload?.Message;
            if (string.IsNullOrWhiteSpace(message))
            {
                message = "رمز عبور با موفقیت تغییر یافت.";
            }

            return ChangePasswordApiResult.Succeeded(message);
        }

        var validationProblem = await TryReadValidationProblemAsync(response, cancellationToken);
        if (validationProblem is not null)
        {
            var fieldErrors = validationProblem.Errors
                .Where(pair => pair.Value.Length > 0)
                .ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyCollection<string>)pair.Value,
                    StringComparer.OrdinalIgnoreCase);

            var message = "ورودی‌های فرم معتبر نیست. لطفا خطاهای فرم را بررسی کنید.";
            if (!string.IsNullOrWhiteSpace(validationProblem.Detail))
            {
                message = validationProblem.Detail;
            }

            return ChangePasswordApiResult.Failed(message, (int)response.StatusCode, fieldErrors);
        }

        var problem = await TryReadProblemDetailsAsync(response, cancellationToken);
        var fallbackMessage = !string.IsNullOrWhiteSpace(problem?.Detail)
            ? problem.Detail
            : GetDefaultMessage(response.StatusCode);
        return ChangePasswordApiResult.Failed(fallbackMessage, (int)response.StatusCode);
    }

    private static string GetDefaultMessage(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => "نشست کاربری معتبر نیست. لطفا دوباره وارد شوید.",
            HttpStatusCode.Forbidden => "شما مجوز انجام این عملیات را ندارید.",
            HttpStatusCode.TooManyRequests => "تعداد درخواست‌های تغییر رمز بیش از حد مجاز است. کمی بعد دوباره تلاش کنید.",
            _ => $"تغییر رمز عبور ناموفق بود. (HTTP {(int)statusCode})"
        };
    }

    private static async Task<ApiResult?> TryReadApiResultAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ApiResult>(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<ValidationProblemDetails?> TryReadValidationProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<ProblemDetails?> TryReadProblemDetailsAsync(
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
