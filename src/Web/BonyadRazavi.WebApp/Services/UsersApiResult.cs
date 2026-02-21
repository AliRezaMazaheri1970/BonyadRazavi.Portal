using BonyadRazavi.Shared.Contracts.Users;
using BonyadRazavi.Shared.Contracts.Companies;

namespace BonyadRazavi.WebApp.Services;

public sealed record UsersApiResult(
    bool IsSuccess,
    IReadOnlyList<UserDto> Users,
    int Page,
    int PageSize,
    int TotalCount,
    string? ErrorMessage,
    int? StatusCode)
{
    public static UsersApiResult Succeeded(
        IReadOnlyList<UserDto> users,
        int page,
        int pageSize,
        int totalCount) =>
        new(true, users, page, pageSize, totalCount, null, null);

    public static UsersApiResult Failed(string errorMessage, int? statusCode = null) =>
        new(false, [], 1, 20, 0, errorMessage, statusCode);
}

public sealed record UserMutationApiResult(
    bool IsSuccess,
    UserDto? User,
    string? ErrorMessage,
    int? StatusCode)
{
    public static UserMutationApiResult Succeeded(UserDto user) =>
        new(true, user, null, null);

    public static UserMutationApiResult Failed(string errorMessage, int? statusCode = null) =>
        new(false, null, errorMessage, statusCode);
}

public sealed record CompanyDirectoryApiResult(
    bool IsSuccess,
    IReadOnlyList<CompanyDto> Companies,
    string? ErrorMessage,
    int? StatusCode)
{
    public static CompanyDirectoryApiResult Succeeded(IReadOnlyList<CompanyDto> companies) =>
        new(true, companies, null, null);

    public static CompanyDirectoryApiResult Failed(string errorMessage, int? statusCode = null) =>
        new(false, [], errorMessage, statusCode);
}

public sealed record CompanyInvoicesApiResult(
    bool IsSuccess,
    IReadOnlyList<CompanyInvoiceDto> Invoices,
    string? ErrorMessage,
    int? StatusCode)
{
    public static CompanyInvoicesApiResult Succeeded(IReadOnlyList<CompanyInvoiceDto> invoices) =>
        new(true, invoices, null, null);

    public static CompanyInvoicesApiResult Failed(string errorMessage, int? statusCode = null) =>
        new(false, [], errorMessage, statusCode);
}

public sealed record InvoicePdfApiResult(
    bool IsSuccess,
    byte[] FileBytes,
    string? FileName,
    string? ErrorMessage,
    int? StatusCode)
{
    public static InvoicePdfApiResult Succeeded(byte[] fileBytes, string? fileName) =>
        new(true, fileBytes, fileName, null, null);

    public static InvoicePdfApiResult Failed(string errorMessage, int? statusCode = null) =>
        new(false, [], null, errorMessage, statusCode);
}
