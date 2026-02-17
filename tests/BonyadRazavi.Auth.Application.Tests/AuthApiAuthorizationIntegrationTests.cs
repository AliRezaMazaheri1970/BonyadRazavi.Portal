using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BonyadRazavi.Auth.Application.Tests;

public sealed class AuthApiAuthorizationIntegrationTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client;

    public AuthApiAuthorizationIntegrationTests(AuthApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMe_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAuditActions_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/audit/actions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCompanies_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/companies");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCompany_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/companies/{Guid.NewGuid()}",
            new
            {
                companyName = "x",
                isActive = true
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable(
            "JWT_SIGNING_KEY",
            "BonyadRazavi-Test-Key-At-Least-32-Characters");

        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:UseSqlServer"] = "false",
                ["Persistence:ApplyMigrationsOnStartup"] = "false",
                ["Jwt:Issuer"] = "BonyadRazavi.Auth.Api",
                ["Jwt:Audience"] = "BonyadRazavi.Portal",
                ["Jwt:AccessTokenLifetimeMinutes"] = "15",
                ["Security:LoginLockout:MaxFailedAttempts"] = "5",
                ["Security:LoginLockout:LockoutMinutes"] = "15",
                ["Security:LoginLockout:EntryTtlMinutes"] = "120",
                ["Security:RefreshTokens:LifetimeDays"] = "7"
            });
        });
    }
}
