using BonyadRazavi.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace BonyadRazavi.WebApp.Services;

public sealed class UserSession
{
    private const string SessionStorageKey = "portal.user.session";
    private readonly ProtectedLocalStorage _storage;
    private bool _isInitialized;

    public UserSession(ProtectedLocalStorage storage)
    {
        _storage = storage;
    }

    public LoginResponse? Current { get; private set; }

    public bool IsAuthenticated => Current is not null;

    public void SignIn(LoginResponse loginResponse)
    {
        Current = loginResponse;
    }

    public void SignOut()
    {
        Current = null;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        try
        {
            var storedSession = await _storage.GetAsync<LoginResponse>(SessionStorageKey);
            if (!storedSession.Success || storedSession.Value is null)
            {
                return;
            }

            Current = storedSession.Value;
        }
        catch
        {
            Current = null;
        }
    }

    public async Task SignInAsync(LoginResponse loginResponse)
    {
        SignIn(loginResponse);

        try
        {
            await _storage.SetAsync(SessionStorageKey, loginResponse);
        }
        catch
        {
            // Best effort persistence. Keep in-memory session even if storage fails.
        }
    }

    public async Task SignOutAsync()
    {
        SignOut();

        try
        {
            await _storage.DeleteAsync(SessionStorageKey);
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
