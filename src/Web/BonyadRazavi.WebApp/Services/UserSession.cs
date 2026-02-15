using BonyadRazavi.Shared.Contracts.Auth;

namespace BonyadRazavi.WebApp.Services;

public sealed class UserSession
{
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
}
