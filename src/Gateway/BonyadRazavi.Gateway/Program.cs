using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using BonyadRazavi.Gateway.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var validateJwtAtGateway = builder.Configuration.GetValue("Security:ValidateJwtAtGateway", true);

if (validateJwtAtGateway)
{
    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
        ?? throw new InvalidOperationException("JWT settings are missing in Gateway.");

    var envSigningKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");
    if (string.IsNullOrWhiteSpace(envSigningKey) || envSigningKey.Length < 32)
    {
        throw new InvalidOperationException("JWT_SIGNING_KEY environment variable is required and must be at least 32 characters.");
    }

    jwtOptions.SigningKey = envSigningKey;

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiJwt", policy =>
    {
        if (validateJwtAtGateway)
        {
            policy.RequireAuthenticatedUser();
            return;
        }

        // In development we delegate JWT enforcement to downstream APIs.
        policy.RequireAssertion(_ => true);
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"login:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    options.AddPolicy("api-standard", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"api:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    options.AddPolicy("refresh", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"refresh:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

var allowedCidrs = builder.Configuration.GetSection("Security:AllowedCidrs").Get<string[]>()
    ?? ["192.168.93.0/27"];
var allowedNetworks = allowedCidrs.Select(ParseIpv4Cidr).ToArray();
var allowLoopbackInDevelopment = app.Environment.IsDevelopment() &&
    builder.Configuration.GetValue<bool>("Security:AllowLoopbackInDevelopment");

var apiAllowPrefixes = builder.Configuration.GetSection("Security:ApiAllowPrefixes").Get<string[]>()
    ?? ["/api/auth"];
var allowedApiPaths = apiAllowPrefixes
    .Where(path => !string.IsNullOrWhiteSpace(path))
    .Select(path => new PathString(path))
    .ToArray();

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    if (!IsIpAllowed(context.Connection.RemoteIpAddress, allowedNetworks, allowLoopbackInDevelopment))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            title = "Forbidden",
            detail = "Client IP address is not allowed."
        });
        return;
    }

    if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
        !IsApiPathAllowed(context.Request.Path, allowedApiPaths))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new
        {
            title = "Not Found",
            detail = "API route is not allowlisted."
        });
        return;
    }

    await next();
});
app.UseRateLimiter();
if (validateJwtAtGateway)
{
    app.UseAuthentication();
}
app.UseAuthorization();

app.MapGet("/gateway/health", () => Results.Ok(new
{
    service = "BonyadRazavi.Gateway",
    status = "Healthy",
    utc = DateTime.UtcNow
}));

app.MapReverseProxy();

app.Run();

static bool IsApiPathAllowed(PathString requestPath, IReadOnlyCollection<PathString> allowedApiPaths)
{
    foreach (var allowedPath in allowedApiPaths)
    {
        if (requestPath.StartsWithSegments(allowedPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

static Ipv4Cidr ParseIpv4Cidr(string cidr)
{
    var parts = cidr.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length != 2)
    {
        throw new InvalidOperationException($"Invalid CIDR value: '{cidr}'.");
    }

    if (!IPAddress.TryParse(parts[0], out var ip))
    {
        throw new InvalidOperationException($"Invalid CIDR IP value: '{parts[0]}'.");
    }

    if (!int.TryParse(parts[1], out var prefixLength) || prefixLength is < 0 or > 32)
    {
        throw new InvalidOperationException($"Invalid CIDR prefix length: '{parts[1]}'.");
    }

    if (ip.IsIPv4MappedToIPv6)
    {
        ip = ip.MapToIPv4();
    }

    if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
    {
        throw new InvalidOperationException("Only IPv4 CIDRs are supported for IP allowlist.");
    }

    var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
    var ipValue = ToUInt32(ip);
    var network = ipValue & mask;

    return new Ipv4Cidr(network, mask);
}

static bool IsIpAllowed(
    IPAddress? remoteIp,
    IReadOnlyCollection<Ipv4Cidr> allowedNetworks,
    bool allowLoopbackInDevelopment)
{
    if (remoteIp is null)
    {
        return false;
    }

    if (allowLoopbackInDevelopment && IPAddress.IsLoopback(remoteIp))
    {
        return true;
    }

    if (remoteIp.IsIPv4MappedToIPv6)
    {
        remoteIp = remoteIp.MapToIPv4();
    }

    if (remoteIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
    {
        return false;
    }

    var ipValue = ToUInt32(remoteIp);
    foreach (var range in allowedNetworks)
    {
        if ((ipValue & range.Mask) == range.Network)
        {
            return true;
        }
    }

    return false;
}

static uint ToUInt32(IPAddress ipAddress)
{
    var bytes = ipAddress.GetAddressBytes();
    if (BitConverter.IsLittleEndian)
    {
        Array.Reverse(bytes);
    }

    return BitConverter.ToUInt32(bytes, 0);
}

readonly record struct Ipv4Cidr(uint Network, uint Mask);
