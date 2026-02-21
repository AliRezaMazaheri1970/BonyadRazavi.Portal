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
