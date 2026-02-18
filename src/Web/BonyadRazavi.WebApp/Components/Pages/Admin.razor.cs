using BonyadRazavi.Shared.Contracts.Users;

namespace BonyadRazavi.WebApp.Components.Pages;

public partial class Admin
{
    private static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromSeconds(30);
    private readonly List<UserDto> _users = [];
    private bool _isLoading;
    private string? _errorMessage;

    private bool IsAdmin =>
        UserSession.Current?.Roles?.Any(role =>
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)) == true;

    protected override async Task OnInitializedAsync()
    {
        if (!UserSession.IsAuthenticated || UserSession.Current is null)
        {
            Navigation.NavigateTo("/login");
            return;
        }

        if (!IsAdmin)
        {
            return;
        }

        await LoadUsersAsync();
    }

    private async Task LoadUsersAsync()
    {
        _isLoading = true;
        _errorMessage = null;

        try
        {
            if (!await EnsureValidAccessTokenAsync())
            {
                return;
            }

            var token = UserSession.Current!.AccessToken;
            var result = await UsersApiClient.GetUsersAsync(token);
            if (!result.IsSuccess && result.StatusCode == StatusCodes.Status401Unauthorized)
            {
                if (!await TryRefreshSessionAsync())
                {
                    return;
                }

                result = await UsersApiClient.GetUsersAsync(UserSession.Current!.AccessToken);
            }

            if (!result.IsSuccess)
            {
                _errorMessage = result.ErrorMessage ?? "دریافت لیست کاربران ناموفق بود.";
                return;
            }

            _users.Clear();
            _users.AddRange(result.Users);
        }
        catch
        {
            _errorMessage = "در ارتباط با سرویس مدیریت کاربران خطا رخ داد.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task<bool> EnsureValidAccessTokenAsync()
    {
        if (UserSession.Current is null)
        {
            _errorMessage = "نشست کاربری معتبر نیست. لطفا دوباره وارد شوید.";
            return false;
        }

        var hasValidAccessToken =
            !string.IsNullOrWhiteSpace(UserSession.Current.AccessToken) &&
            UserSession.Current.ExpiresAtUtc > DateTime.UtcNow.Add(TokenRefreshSkew);

        if (hasValidAccessToken)
        {
            return true;
        }

        return await TryRefreshSessionAsync();
    }

    private async Task<bool> TryRefreshSessionAsync()
    {
        var refreshToken = UserSession.Current?.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _errorMessage = "نشست کاربری معتبر نیست. لطفا دوباره وارد شوید.";
            return false;
        }

        var refreshResult = await AuthApiClient.RefreshAsync(refreshToken);
        if (!refreshResult.IsSuccess || refreshResult.Payload is null)
        {
            _errorMessage = refreshResult.ErrorMessage ?? "نشست کاربری معتبر نیست. لطفا دوباره وارد شوید.";
            return false;
        }

        UserSession.SignIn(refreshResult.Payload);
        return true;
    }

    private void GoToLogin()
    {
        Navigation.NavigateTo("/login");
    }

    private void GoToDashboard()
    {
        Navigation.NavigateTo("/dashboard");
    }
}
