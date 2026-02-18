using System.Text;
using System.Threading.RateLimiting;
using BonyadRazavi.Auth.Api.Audit;
using BonyadRazavi.Auth.Api.Observability;
using BonyadRazavi.Auth.Api.Security;
using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Constants;
using BonyadRazavi.Auth.Application.Services;
using BonyadRazavi.Auth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ProblemDetailsCorrelationFilter>();
});
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["correlationId"] =
            CorrelationIdMiddleware.GetCorrelationId(context.HttpContext);
    };
});
builder.Services.AddOpenApi();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<LoginLockoutOptions>(builder.Configuration.GetSection(LoginLockoutOptions.SectionName));
builder.Services.Configure<LoginFailureAlertOptions>(builder.Configuration.GetSection(LoginFailureAlertOptions.SectionName));
builder.Services.Configure<PasswordPolicyOptions>(builder.Configuration.GetSection(PasswordPolicyOptions.SectionName));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT settings are missing.");
var envSigningKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");
if (string.IsNullOrWhiteSpace(envSigningKey) || envSigningKey.Length < 32)
{
    throw new InvalidOperationException("JWT_SIGNING_KEY environment variable is required and must be at least 32 characters.");
}
jwtOptions.SigningKey = envSigningKey;
builder.Services.PostConfigure<JwtOptions>(options =>
{
    options.SigningKey = envSigningKey;
});
if (builder.Environment.IsProduction() &&
    string.Equals(jwtOptions.SigningKey, "Razavi-Portal-Dev-Key-ReplaceBeforeProd-AtLeast32Chars", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Development JWT signing key is not allowed in production.");
}

builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddAuthInfrastructure(builder.Configuration);
builder.Services.AddSingleton<JwtTokenFactory>();
builder.Services.AddSingleton<ILoginLockoutService, InMemoryLoginLockoutService>();
builder.Services.AddSingleton<ILoginFailureAlertService, InMemoryLoginFailureAlertService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.IncludeErrorDetails = builder.Environment.IsDevelopment();
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrWhiteSpace(context.Token) &&
                    context.Request.Headers.TryGetValue("X-Access-Token", out var tokenValues))
                {
                    var fallbackToken = tokenValues.ToString();
                    if (!string.IsNullOrWhiteSpace(fallbackToken))
                    {
                        context.Token = fallbackToken;
                    }
                }

                return Task.CompletedTask;
            }
        };
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

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("change-password", context =>
    {
        var ip = RequestAuditMetadataFactory.ResolveClientIp(context);
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"change-password:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            });
    });
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AuthorizationPolicies.AuthenticatedUser,
        policy => policy.RequireAuthenticatedUser());

    options.AddPolicy(
        AuthorizationPolicies.PortalAccess,
        policy => policy.RequireRole(
            PortalRoles.SuperAdmin,
            PortalRoles.Admin,
            PortalRoles.CompanyAdmin,
            PortalRoles.Manager,
            PortalRoles.User,
            PortalRoles.Auditor,
            PortalRoles.Operator));

    options.AddPolicy(
        AuthorizationPolicies.UsersRead,
        policy => policy.RequireRole(
            PortalRoles.SuperAdmin,
            PortalRoles.Admin,
            PortalRoles.CompanyAdmin,
            PortalRoles.Manager));

    options.AddPolicy(
        AuthorizationPolicies.UsersManage,
        policy => policy.RequireRole(
            PortalRoles.SuperAdmin,
            PortalRoles.Admin,
            PortalRoles.CompanyAdmin));

    options.AddPolicy(
        AuthorizationPolicies.CompaniesRead,
        policy => policy.RequireRole(
            PortalRoles.SuperAdmin,
            PortalRoles.Admin,
            PortalRoles.CompanyAdmin,
            PortalRoles.Manager,
            PortalRoles.Operator));

    options.AddPolicy(
        AuthorizationPolicies.CompaniesManage,
        policy => policy.RequireRole(
            PortalRoles.SuperAdmin,
            PortalRoles.Admin,
            PortalRoles.CompanyAdmin));

    options.AddPolicy(
        AuthorizationPolicies.AuditRead,
        policy => policy.RequireRole(
            PortalRoles.SuperAdmin,
            PortalRoles.Admin,
            PortalRoles.Auditor));

    options.AddPolicy(
        AuthorizationPolicies.SystemAdmin,
        policy => policy.RequireRole(
            PortalRoles.SuperAdmin,
            PortalRoles.Admin));
});

var app = builder.Build();
await app.Services.ApplyAuthMigrationsIfEnabledAsync();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalExceptionHandler");
        var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);

        logger.LogError(
            exceptionFeature?.Error,
            "Unhandled exception. CorrelationId: {CorrelationId}",
            correlationId);

        var problem = new ProblemDetails
        {
            Title = "خطای غیرمنتظره در سرویس",
            Detail = "در پردازش درخواست خطایی رخ داد. کد پیگیری را برای پشتیبانی ارسال کنید.",
            Status = StatusCodes.Status500InternalServerError
        };
        problem.Extensions["correlationId"] = correlationId;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    });
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    await next();

    if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    if (context.Request.Path.StartsWithSegments("/api/auth/login", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    if (context.Response.StatusCode is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden))
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var userActionLogService = scope.ServiceProvider.GetRequiredService<IUserActionLogService>();
    await userActionLogService.LogAsync(
        RequestAuditMetadataFactory.ResolveAuthenticatedUserId(context.User),
        AuditActionTypes.SecurityDenied,
        RequestAuditMetadataFactory.Create(context, new Dictionary<string, object?>
        {
            ["statusCode"] = context.Response.StatusCode
        }));
});

app.MapGet("/health", async (
        HttpContext context,
        IUserActionLogService userActionLogService) =>
    {
        await userActionLogService.LogAsync(
            RequestAuditMetadataFactory.ResolveAuthenticatedUserId(context.User),
            AuditActionTypes.ServiceHealth,
            RequestAuditMetadataFactory.Create(context));

        return Results.Ok(new
        {
            service = "BonyadRazavi.Auth.Api",
            status = "Healthy",
            utc = DateTime.UtcNow
        });
    })
    .RequireAuthorization(AuthorizationPolicies.SystemAdmin);
app.MapControllers();

app.Run();

public partial class Program;
