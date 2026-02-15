using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Models;
using BonyadRazavi.Shared.Contracts.Auth;

namespace BonyadRazavi.Auth.Application.Services;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public AuthenticationService(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserName = request.UserName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthenticationResult.Fail("نام کاربری یا کلمه عبور معتبر نیست.");
        }

        var user = await _userRepository.FindByUserNameAsync(normalizedUserName, cancellationToken);
        if (user is null || !_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            return AuthenticationResult.Fail("نام کاربری یا کلمه عبور اشتباه است.");
        }

        return AuthenticationResult.Success(
            new AuthenticatedUser(user.Id, user.UserName, user.DisplayName, user.Roles));
    }
}
