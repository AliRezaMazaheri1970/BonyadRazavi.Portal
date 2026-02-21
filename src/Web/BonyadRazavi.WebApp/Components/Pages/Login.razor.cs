using BonyadRazavi.Shared.Contracts.Auth;
using BonyadRazavi.WebApp.Services;

namespace BonyadRazavi.WebApp.Components.Pages
{
    public partial class Login
    {
        private readonly LoginRequest _model = new();
        private bool _isSubmitting;
        private string? _errorMessage;

        protected override async Task OnInitializedAsync()
        {
            await UserSession.InitializeAsync();

            var relativePath = Navigation.ToBaseRelativePath(Navigation.Uri);
            var pathWithoutQuery = relativePath.Split('?', 2)[0].Trim('/').ToLowerInvariant();
            var isExplicitLoginRoute = pathWithoutQuery == "login";

            var hasUsableSession =
                UserSession.Current is not null &&
                !string.IsNullOrWhiteSpace(UserSession.Current.AccessToken) &&
                UserSession.Current.ExpiresAtUtc > DateTime.UtcNow.AddSeconds(30);

            if (hasUsableSession && !isExplicitLoginRoute)
            {
                Navigation.NavigateTo("/dashboard");
                return;
            }

            if (!hasUsableSession && UserSession.IsAuthenticated)
            {
                await UserSession.SignOutAsync();
            }
        }

        private async Task OnSubmitAsync()
        {
            _errorMessage = null;
            _isSubmitting = true;

            try
            {
                var loginRequest = new LoginRequest
                {
                    UserName = _model.UserName.Trim(),
                    Password = _model.Password
                };

                var loginResult = await AuthApiClient.LoginAsync(loginRequest);
                if (loginResult.IsSuccess && loginResult.Payload is not null)
                {
                    await UserSession.SignInAsync(loginResult.Payload);
                    Navigation.NavigateTo("/dashboard");
                    return;
                }

                _errorMessage = loginResult.ErrorMessage ?? "ورود ناموفق بود.";
            }
            catch
            {
                _errorMessage = "ارتباط با سرویس برقرار نشد. لطفا سرویس Gateway را بررسی کنید.";
            }
            finally
            {
                _isSubmitting = false;
            }
        }
    }
}
