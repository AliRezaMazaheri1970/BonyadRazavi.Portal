using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BonyadRazavi.Auth.Application.Models;
using BonyadRazavi.Shared.Contracts.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BonyadRazavi.Auth.Api.Security;

public sealed class JwtTokenFactory
{
    private readonly JwtOptions _options;

    public JwtTokenFactory(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public LoginResponse Create(AuthenticatedUser user)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("display_name", user.DisplayName),
            new("company_code", user.CompanyCode.ToString())
        };
        if (!string.IsNullOrWhiteSpace(user.CompanyName))
        {
            claims.Add(new Claim("company_name", user.CompanyName));
        }
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: signingCredentials);

        var rawToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new LoginResponse
        {
            AccessToken = rawToken,
            ExpiresAtUtc = expiresAt,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Roles = user.Roles.ToList(),
            CompanyCode = user.CompanyCode,
            CompanyName = user.CompanyName
        };
    }
}
