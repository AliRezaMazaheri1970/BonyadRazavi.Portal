using BonyadRazavi.Auth.Application.Models;
using BonyadRazavi.Shared.Contracts.Auth;

namespace BonyadRazavi.Auth.Application.Abstractions;

public interface IAuthenticationService
{
    Task<AuthenticationResult> AuthenticateAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}
