using BonyadRazavi.Shared.Contracts.Companies;
using BonyadRazavi.Shared.Contracts.Users;
using BonyadRazavi.WebApp.Services;
using Microsoft.AspNetCore.Components;

namespace BonyadRazavi.WebApp.Components.Pages;

public partial class Admin
{
    private static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromSeconds(30);
    private static readonly string[] EditableRoles =
    [
        "SuperAdmin",
        "Admin",
        "CompanyAdmin",
        "Manager",
        "User",
        "Auditor",
        "Operator"
    ];

    private const int DefaultPageSize = 10;

    private readonly List<UserDto> _users = [];
    private readonly HashSet<string> _editorRoles = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CompanyDto> _companyDirectory = [];

    private bool _isLoading;
    private bool _isEditorOpen;
    private bool _isSavingUser;
    private bool _companyDirectoryLoaded;
    private bool _isLoadingCompanyDirectory;
    private bool _isCompanyPickerOpen;
    private string? _errorMessage;
    private string? _searchTerm;
    private string? _companySearchTerm;
    private int _currentPage = 1;
    private int _pageSize = DefaultPageSize;
    private int _totalCount;

    private Guid? _editingUserId;
    private string _editorUserName = string.Empty;
    private string _editorDisplayName = string.Empty;
    private string _editorPassword = string.Empty;
    private string _editorCompanyCode = string.Empty;
    private string _editorCompanyName = string.Empty;
    private bool _editorIsActive = true;
    private bool _editorIsCompanyActive = true;
    private string? _userEditorError;

    private bool IsEditMode => _editingUserId.HasValue;
    private string UserEditorTitle => IsEditMode ? "اصلاح کاربر" : "افزودن کاربر جدید";
    private string SaveButtonTitle =>
        _isSavingUser
            ? "در حال ذخیره..."
            : IsEditMode ? "ثبت اصلاحات" : "ایجاد کاربر";
    private bool CanGoToPreviousPage => _currentPage > 1;
    private bool CanGoToNextPage => _currentPage * _pageSize < _totalCount;
    private string SelectedCompanyName => string.IsNullOrWhiteSpace(_editorCompanyName) ? "-" : _editorCompanyName;
    private IReadOnlyList<CompanyDto> FilteredCompanies
    {
        get
        {
            var normalizedSearch = _companySearchTerm?.Trim();
            IEnumerable<CompanyDto> query = _companyDirectory;

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(company =>
                    (!string.IsNullOrWhiteSpace(company.CompanyName) &&
                     company.CompanyName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)) ||
                    company.CompanyCode.ToString("D").Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
            }

            return query
                .OrderBy(company => company.CompanyName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(company => company.CompanyCode)
                .ToList();
        }
    }

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
        await EnsureCompanyDirectoryLoadedAsync();
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
            var result = await UsersApiClient.GetUsersAsync(
                token,
                _searchTerm,
                _currentPage,
                _pageSize);

            if (!result.IsSuccess && result.StatusCode == StatusCodes.Status401Unauthorized)
            {
                if (!await TryRefreshSessionAsync())
                {
                    return;
                }

                result = await UsersApiClient.GetUsersAsync(
                    UserSession.Current!.AccessToken,
                    _searchTerm,
                    _currentPage,
                    _pageSize);
            }

            if (!result.IsSuccess)
            {
                _errorMessage = result.ErrorMessage ?? "دریافت لیست کاربران ناموفق بود.";
                return;
            }

            _users.Clear();
            _users.AddRange(result.Users);
            _currentPage = result.Page;
            _pageSize = result.PageSize;
            _totalCount = result.TotalCount;
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

    private async Task EnsureCompanyDirectoryLoadedAsync()
    {
        if (_companyDirectoryLoaded || _isLoadingCompanyDirectory)
        {
            return;
        }

        _isLoadingCompanyDirectory = true;
        try
        {
            if (!await EnsureValidAccessTokenAsync())
            {
                return;
            }

            var token = UserSession.Current!.AccessToken;
            var result = await UsersApiClient.GetCompanyDirectoryAsync(token);
            if (!result.IsSuccess && result.StatusCode == StatusCodes.Status401Unauthorized)
            {
                if (!await TryRefreshSessionAsync())
                {
                    return;
                }

                result = await UsersApiClient.GetCompanyDirectoryAsync(UserSession.Current!.AccessToken);
            }

            if (!result.IsSuccess)
            {
                _errorMessage = result.ErrorMessage ?? "دریافت لیست شرکت‌ها ناموفق بود.";
                return;
            }

            _companyDirectory.Clear();
            _companyDirectory.AddRange(result.Companies);
            _companyDirectoryLoaded = true;
            SyncEditorCompanyName();
        }
        catch
        {
            _errorMessage = "در دریافت اطلاعات شرکت‌ها خطا رخ داد.";
        }
        finally
        {
            _isLoadingCompanyDirectory = false;
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

    private async Task ApplySearchAsync()
    {
        _currentPage = 1;
        await LoadUsersAsync();
    }

    private async Task GoToPreviousPageAsync()
    {
        if (!CanGoToPreviousPage || _isLoading)
        {
            return;
        }

        _currentPage--;
        await LoadUsersAsync();
    }

    private async Task GoToNextPageAsync()
    {
        if (!CanGoToNextPage || _isLoading)
        {
            return;
        }

        _currentPage++;
        await LoadUsersAsync();
    }

    private async Task OpenCreateUserDialog()
    {
        await EnsureCompanyDirectoryLoadedAsync();

        _editingUserId = null;
        _editorUserName = string.Empty;
        _editorDisplayName = string.Empty;
        _editorPassword = string.Empty;
        _editorCompanyCode = UserSession.Current?.CompanyCode is Guid sessionCompanyCode && sessionCompanyCode != Guid.Empty
            ? sessionCompanyCode.ToString("D")
            : string.Empty;
        _editorCompanyName = UserSession.Current?.CompanyName ?? string.Empty;
        _editorIsActive = true;
        _editorIsCompanyActive = true;
        _userEditorError = null;
        _isCompanyPickerOpen = false;
        _companySearchTerm = string.Empty;

        _editorRoles.Clear();
        _editorRoles.Add("User");

        SyncEditorCompanyName();
        _isEditorOpen = true;
    }

    private async Task OpenEditUserDialog(UserDto user)
    {
        await EnsureCompanyDirectoryLoadedAsync();

        _editingUserId = user.UserId;
        _editorUserName = user.UserName;
        _editorDisplayName = user.DisplayName;
        _editorPassword = string.Empty;
        _editorCompanyCode = user.CompanyCode == Guid.Empty ? string.Empty : user.CompanyCode.ToString("D");
        _editorCompanyName = user.CompanyName ?? string.Empty;
        _editorIsActive = user.IsActive;
        _editorIsCompanyActive = user.IsCompanyActive;
        _userEditorError = null;
        _isCompanyPickerOpen = false;
        _companySearchTerm = string.Empty;

        _editorRoles.Clear();
        foreach (var role in user.Roles.Where(role => !string.IsNullOrWhiteSpace(role)))
        {
            _editorRoles.Add(role.Trim());
        }

        if (_editorRoles.Count == 0)
        {
            _editorRoles.Add("User");
        }

        SyncEditorCompanyName();
        _isEditorOpen = true;
    }

    private void CloseUserDialog()
    {
        if (_isSavingUser)
        {
            return;
        }

        _isEditorOpen = false;
        _userEditorError = null;
        _isCompanyPickerOpen = false;
        _companySearchTerm = string.Empty;
    }

    private async Task SaveUserAsync()
    {
        if (_isSavingUser)
        {
            return;
        }

        if (!TryValidateEditor(out var companyCode))
        {
            return;
        }

        if (!await EnsureValidAccessTokenAsync())
        {
            return;
        }

        _isSavingUser = true;
        _userEditorError = null;

        try
        {
            var result = await SaveUserWithCurrentTokenAsync(companyCode);
            if (!result.IsSuccess && result.StatusCode == StatusCodes.Status401Unauthorized)
            {
                if (!await TryRefreshSessionAsync())
                {
                    return;
                }

                result = await SaveUserWithCurrentTokenAsync(companyCode);
            }

            if (!result.IsSuccess)
            {
                _userEditorError = result.ErrorMessage ?? "ذخیره اطلاعات کاربر ناموفق بود.";
                return;
            }

            _isEditorOpen = false;
            _isCompanyPickerOpen = false;
            _companySearchTerm = string.Empty;
            await LoadUsersAsync();
        }
        catch
        {
            _userEditorError = "در ذخیره اطلاعات کاربر خطا رخ داد.";
        }
        finally
        {
            _isSavingUser = false;
        }
    }

    private async Task<UserMutationApiResult> SaveUserWithCurrentTokenAsync(Guid companyCode)
    {
        var token = UserSession.Current!.AccessToken;
        var selectedRoles = _editorRoles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        SyncEditorCompanyName();
        var companyName = string.IsNullOrWhiteSpace(_editorCompanyName) ? null : _editorCompanyName.Trim();

        if (IsEditMode)
        {
            var updateRequest = new UpdateUserRequest
            {
                DisplayName = _editorDisplayName.Trim(),
                Password = string.IsNullOrWhiteSpace(_editorPassword) ? null : _editorPassword,
                Roles = selectedRoles,
                CompanyCode = companyCode,
                CompanyName = companyName,
                IsActive = _editorIsActive,
                IsCompanyActive = _editorIsCompanyActive
            };

            return await UsersApiClient.UpdateUserAsync(
                token,
                _editingUserId!.Value,
                updateRequest);
        }

        var createRequest = new CreateUserRequest
        {
            UserName = _editorUserName.Trim(),
            DisplayName = _editorDisplayName.Trim(),
            Password = _editorPassword,
            Roles = selectedRoles,
            CompanyCode = companyCode,
            CompanyName = companyName,
            IsActive = _editorIsActive,
            IsCompanyActive = _editorIsCompanyActive
        };

        return await UsersApiClient.CreateUserAsync(token, createRequest);
    }

    private bool TryValidateEditor(out Guid companyCode)
    {
        companyCode = Guid.Empty;
        _userEditorError = null;

        if (!IsEditMode && string.IsNullOrWhiteSpace(_editorUserName))
        {
            _userEditorError = "نام کاربری الزامی است.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_editorDisplayName))
        {
            _userEditorError = "نام نمایشی الزامی است.";
            return false;
        }

        if (!IsEditMode)
        {
            if (string.IsNullOrWhiteSpace(_editorPassword))
            {
                _userEditorError = "رمز عبور اولیه الزامی است.";
                return false;
            }

            if (_editorPassword.Length < 8)
            {
                _userEditorError = "رمز عبور باید حداقل 8 کاراکتر باشد.";
                return false;
            }
        }
        else if (!string.IsNullOrWhiteSpace(_editorPassword) && _editorPassword.Length < 8)
        {
            _userEditorError = "در صورت تغییر رمز، طول آن باید حداقل 8 کاراکتر باشد.";
            return false;
        }

        if (_editorRoles.Count == 0)
        {
            _userEditorError = "حداقل یک نقش باید انتخاب شود.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_editorCompanyCode))
        {
            companyCode = Guid.Empty;
            return true;
        }

        if (Guid.TryParse(_editorCompanyCode.Trim(), out companyCode))
        {
            return true;
        }

        _userEditorError = "شرکت انتخاب‌شده معتبر نیست.";
        return false;
    }

    private async Task ToggleCompanyPickerAsync()
    {
        if (_isCompanyPickerOpen)
        {
            _isCompanyPickerOpen = false;
            _companySearchTerm = string.Empty;
            return;
        }

        await EnsureCompanyDirectoryLoadedAsync();
        _isCompanyPickerOpen = true;
    }

    private void SelectCompany(CompanyDto company)
    {
        _editorCompanyCode = company.CompanyCode.ToString("D");
        _editorCompanyName = company.CompanyName ?? string.Empty;
        _isCompanyPickerOpen = false;
        _companySearchTerm = string.Empty;
        _userEditorError = null;
    }

    private bool IsSelectedCompany(CompanyDto company)
    {
        return Guid.TryParse(_editorCompanyCode, out var selectedCompanyCode) &&
               selectedCompanyCode == company.CompanyCode;
    }

    private static string GetCompanyLabel(CompanyDto company)
    {
        return string.IsNullOrWhiteSpace(company.CompanyName)
            ? company.CompanyCode.ToString("D")
            : company.CompanyName;
    }

    private bool IsRoleSelected(string role)
    {
        return _editorRoles.Contains(role);
    }

    private void ToggleRole(string role, ChangeEventArgs args)
    {
        var rawValue = args.Value?.ToString();
        var isChecked = string.Equals(rawValue, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(rawValue, "on", StringComparison.OrdinalIgnoreCase);

        if (isChecked)
        {
            _editorRoles.Add(role);
        }
        else
        {
            _editorRoles.Remove(role);
        }
    }

    private void SyncEditorCompanyName()
    {
        if (!Guid.TryParse(_editorCompanyCode, out var companyCode))
        {
            if (string.IsNullOrWhiteSpace(_editorCompanyCode))
            {
                _editorCompanyName = string.Empty;
            }

            return;
        }

        var selected = _companyDirectory.FirstOrDefault(company => company.CompanyCode == companyCode);
        if (selected is not null)
        {
            _editorCompanyName = selected.CompanyName ?? string.Empty;
            return;
        }

        if (string.IsNullOrWhiteSpace(_editorCompanyName))
        {
            _editorCompanyName = "نام شرکت یافت نشد";
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
