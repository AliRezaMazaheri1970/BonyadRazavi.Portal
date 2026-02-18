using System.Reflection;
using BonyadRazavi.Auth.Api.Controllers;
using BonyadRazavi.Auth.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace BonyadRazavi.Auth.Application.Tests;

public sealed class AccountControllerSecurityTests
{
    [Fact]
    public void ChangePassword_UsesAuthenticatedPolicyAndRateLimit()
    {
        var classAuthorize = typeof(AccountController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(classAuthorize);
        Assert.Equal(AuthorizationPolicies.AuthenticatedUser, classAuthorize!.Policy);

        var method = typeof(AccountController).GetMethod(nameof(AccountController.ChangePassword));
        Assert.NotNull(method);

        var rateLimitAttribute = method!.GetCustomAttribute<EnableRateLimitingAttribute>();
        Assert.NotNull(rateLimitAttribute);
        Assert.Equal("change-password", rateLimitAttribute!.PolicyName);
    }
}
