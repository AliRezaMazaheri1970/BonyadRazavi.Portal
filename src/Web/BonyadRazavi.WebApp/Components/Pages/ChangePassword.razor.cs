using BonyadRazavi.Shared.Contracts.Account;

namespace BonyadRazavi.WebApp.Components.Pages;

public partial class ChangePassword
{
    private readonly ChangePasswordRequest _model = new();
    private readonly Dictionary<string, IReadOnlyCollection<string>> _fieldErrors =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _isSubmitting;
    private string? _successMessage;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await UserSession.InitializeAsync();

        if (!UserSession.IsAuthenticated)
        {
            Navigation.NavigateTo("/login");
        }
    }

    private async Task SubmitAsync()
    {
        _isSubmitting = true;
        _errorMessage = null;
        _successMessage = null;
        _fieldErrors.Clear();

        try
        {
            var token = UserSession.Current?.AccessToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                _errorMessage = "نشست کاربری معتبر نیست. لطفا دوباره وارد شوید.";
                return;
            }

            var result = await ChangePasswordApiClient.ChangePasswordAsync(token, _model);
            if (!result.IsSuccess)
            {
                _errorMessage = result.Message;
                foreach (var (field, messages) in result.FieldErrors)
                {
                    _fieldErrors[field] = messages;
                }

                return;
            }

            _successMessage = result.Message;
            _model.CurrentPassword = string.Empty;
            _model.NewPassword = string.Empty;
            _model.ConfirmNewPassword = string.Empty;
        }
        catch
        {
            _errorMessage = "در ارتباط با سرویس تغییر رمز خطا رخ داد.";
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private IReadOnlyCollection<string> GetFieldErrors(string fieldName)
    {
        return _fieldErrors.TryGetValue(fieldName, out var errors)
            ? errors
            : [];
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
