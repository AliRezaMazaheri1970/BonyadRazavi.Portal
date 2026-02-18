using BonyadRazavi.Shared.Contracts.Users;

namespace BonyadRazavi.WebApp.Services;

public sealed record UsersApiResult(
    bool IsSuccess,
    IReadOnlyList<UserDto> Users,
    string? ErrorMessage,
    int? StatusCode)
{
    public static UsersApiResult Succeeded(IReadOnlyList<UserDto> users) =>
        new(true, users, null, null);

    public static UsersApiResult Failed(string errorMessage, int? statusCode = null) =>
        new(false, [], errorMessage, statusCode);
}
