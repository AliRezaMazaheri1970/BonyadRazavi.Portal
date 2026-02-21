using System.Net.Http.Headers;
using System.Net.Http.Json;
using BonyadRazavi.Shared.Contracts.Companies;
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
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var requestUri = $"api/users/?page={page}&pageSize={pageSize}";
        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            requestUri += $"&search={Uri.EscapeDataString(normalizedSearch)}";
        }

        using var request = CreateAuthorizedJsonRequest(HttpMethod.Get, requestUri, accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var pagedResult = await TryReadPagedUsersResponseAsync(response, cancellationToken);
            if (pagedResult is not null)
            {
                return UsersApiResult.Succeeded(
                    pagedResult.Items,
                    pagedResult.Page,
                    pagedResult.PageSize,
                    pagedResult.TotalCount);
            }

            var users = await response.Content.ReadFromJsonAsync<List<UserDto>>(cancellationToken);
            var fallbackUsers = users ?? [];
            return UsersApiResult.Succeeded(fallbackUsers, 1, fallbackUsers.Count, fallbackUsers.Count);
        }

        var message = await ReadFailureMessageAsync(
            response,
            "دریافت لیست کاربران ناموفق بود.",
            cancellationToken);
        return UsersApiResult.Failed(message, (int)response.StatusCode);
    }

    public async Task<UserMutationApiResult> CreateUserAsync(
        string accessToken,
        CreateUserRequest requestPayload,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateAuthorizedJsonRequest(
            HttpMethod.Post,
            "api/users",
            accessToken,
            requestPayload);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var createdUser = await response.Content.ReadFromJsonAsync<UserDto>(cancellationToken);
            if (createdUser is not null)
            {
                return UserMutationApiResult.Succeeded(createdUser);
            }

            return UserMutationApiResult.Failed("پاسخ سرویس برای ساخت کاربر نامعتبر است.");
        }

        var message = await ReadFailureMessageAsync(
            response,
            "ساخت کاربر ناموفق بود.",
            cancellationToken);
        return UserMutationApiResult.Failed(message, (int)response.StatusCode);
    }

    public async Task<UserMutationApiResult> UpdateUserAsync(
        string accessToken,
        Guid userId,
        UpdateUserRequest requestPayload,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateAuthorizedJsonRequest(
            HttpMethod.Put,
            $"api/users/{userId:D}",
            accessToken,
            requestPayload);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var updatedUser = await response.Content.ReadFromJsonAsync<UserDto>(cancellationToken);
            if (updatedUser is not null)
            {
                return UserMutationApiResult.Succeeded(updatedUser);
            }

            return UserMutationApiResult.Failed("پاسخ سرویس برای ویرایش کاربر نامعتبر است.");
        }

        var message = await ReadFailureMessageAsync(
            response,
            "ویرایش کاربر ناموفق بود.",
            cancellationToken);
        return UserMutationApiResult.Failed(message, (int)response.StatusCode);
    }

    public async Task<CompanyDirectoryApiResult> GetCompanyDirectoryAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateAuthorizedJsonRequest(HttpMethod.Get, "api/companies/directory", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var companies = await response.Content.ReadFromJsonAsync<List<CompanyDto>>(cancellationToken);
            return CompanyDirectoryApiResult.Succeeded(companies ?? []);
        }

        var message = await ReadFailureMessageAsync(
            response,
            "لیست شرکت‌ها قابل دریافت نیست.",
            cancellationToken);
        return CompanyDirectoryApiResult.Failed(message, (int)response.StatusCode);
    }

    public async Task<CompanyInvoicesApiResult> GetCompanyInvoicesAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateAuthorizedJsonRequest(HttpMethod.Get, "api/companies/invoices", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var invoices = await response.Content.ReadFromJsonAsync<List<CompanyInvoiceDto>>(cancellationToken);
            return CompanyInvoicesApiResult.Succeeded(invoices ?? []);
        }

        var message = await ReadFailureMessageAsync(
            response,
            "دریافت لیست صورتحساب‌ها ناموفق بود.",
            cancellationToken);
        return CompanyInvoicesApiResult.Failed(message, (int)response.StatusCode);
    }

    public async Task<InvoicePdfApiResult> DownloadCompanyInvoicePdfAsync(
        string accessToken,
        Guid masterBillCode,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateAuthorizedJsonRequest(
            HttpMethod.Get,
            $"api/companies/invoices/{masterBillCode:D}/pdf",
            accessToken);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var fileBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentDisposition = response.Content.Headers.ContentDisposition;
            var fileName = contentDisposition?.FileNameStar
                ?? contentDisposition?.FileName?.Trim('"');

            return InvoicePdfApiResult.Succeeded(fileBytes, fileName);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return InvoicePdfApiResult.Failed(
                "صورتحساب مورد نظر یافت نشد یا دسترسی لازم برای دانلود آن را ندارید.",
                (int)response.StatusCode);
        }

        var message = await ReadFailureMessageAsync(
            response,
            "دریافت فایل صورتحساب ناموفق بود.",
            cancellationToken);
        return InvoicePdfApiResult.Failed(message, (int)response.StatusCode);
    }

    private static HttpRequestMessage CreateAuthorizedJsonRequest(
        HttpMethod method,
        string requestUri,
        string accessToken,
        object? payload = null)
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("X-Access-Token", accessToken);

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        return request;
    }

    private static async Task<PagedUsersResponse?> TryReadPagedUsersResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<PagedUsersResponse>(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<ValidationProblemDetails?> ReadValidationProblemDetailsAsync(
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

    private static async Task<string> ReadFailureMessageAsync(
        HttpResponseMessage response,
        string defaultMessage,
        CancellationToken cancellationToken)
    {
        var validationProblem = await ReadValidationProblemDetailsAsync(response, cancellationToken);
        if (validationProblem is not null && validationProblem.Errors.Count > 0)
        {
            var errors = validationProblem.Errors
                .SelectMany(entry => entry.Value ?? [])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (errors.Length > 0)
            {
                return string.Join(" ", errors);
            }
        }

        var problem = await ReadProblemDetailsAsync(response, cancellationToken);
        return BuildFailureMessage(response, problem, defaultMessage);
    }

    private static string BuildFailureMessage(
        HttpResponseMessage response,
        ProblemDetails? problem,
        string defaultMessage)
    {
        var authChallenge = response.Headers.WwwAuthenticate.Count > 0
            ? string.Join(" | ", response.Headers.WwwAuthenticate.Select(header => header.ToString()))
            : null;

        var problemMessage = problem?.Detail;
        if (string.IsNullOrWhiteSpace(problemMessage))
        {
            problemMessage = problem?.Title;
        }

        if (!string.IsNullOrWhiteSpace(problemMessage))
        {
            if (!string.IsNullOrWhiteSpace(authChallenge))
            {
                return $"{problemMessage} ({authChallenge})";
            }

            return problemMessage;
        }

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                !string.IsNullOrWhiteSpace(authChallenge)
                    ? $"نشست کاربری معتبر نیست. لطفا دوباره وارد شوید. ({authChallenge})"
                    : "نشست کاربری معتبر نیست. لطفا دوباره وارد شوید.",
            System.Net.HttpStatusCode.Forbidden => "شما مجوز انجام این عملیات را ندارید.",
            System.Net.HttpStatusCode.NotFound => "سرویس مدیریت کاربران در دسترس نیست.",
            _ => $"{defaultMessage} (HTTP {(int)response.StatusCode})"
        };
    }
}
