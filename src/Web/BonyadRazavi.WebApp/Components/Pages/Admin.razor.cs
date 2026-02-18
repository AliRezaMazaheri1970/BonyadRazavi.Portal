using BonyadRazavi.Shared.Contracts.Users;

namespace BonyadRazavi.WebApp.Components.Pages;

public partial class Admin
{
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
            var token = UserSession.Current?.AccessToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                _errorMessage = "توکن دسترسی معتبر نیست. لطفا دوباره وارد شوید.";
                return;
            }

            var result = await UsersApiClient.GetUsersAsync(token);
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

    private void GoToLogin()
    {
        Navigation.NavigateTo("/login");
    }

    private void GoToDashboard()
    {
        Navigation.NavigateTo("/dashboard");
    }
}
