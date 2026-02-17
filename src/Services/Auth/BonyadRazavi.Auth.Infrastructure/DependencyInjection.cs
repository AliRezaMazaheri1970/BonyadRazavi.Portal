using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PersistenceOptions>(configuration.GetSection(PersistenceOptions.SectionName));
        services.Configure<CompanyDirectoryOptions>(configuration.GetSection(CompanyDirectoryOptions.SectionName));
        services.Configure<RefreshTokenOptions>(configuration.GetSection(RefreshTokenOptions.SectionName));
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        var persistenceOptions = configuration.GetSection(PersistenceOptions.SectionName).Get<PersistenceOptions>()
            ?? new PersistenceOptions();

        if (!persistenceOptions.UseSqlServer)
        {
            services.AddDbContext<AuthDbContext>(options => options.UseInMemoryDatabase("BonyadRazavi.Auth.InMemory"));
            services.AddSingleton<IUserRepository, InMemoryUserRepository>();
            services.AddSingleton<ICompanyDirectoryService, InMemoryCompanyDirectoryService>();
            services.AddSingleton<IUserActionLogService, NoOpUserActionLogService>();
            services.AddSingleton<IRefreshTokenService, InMemoryRefreshTokenService>();
            return services;
        }

        var environmentConnectionString = Environment.GetEnvironmentVariable("AUTH_DB_CONNECTION_STRING");
        var connectionString = !string.IsNullOrWhiteSpace(environmentConnectionString)
            ? environmentConnectionString
            : configuration.GetConnectionString(persistenceOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{persistenceOptions.ConnectionStringName}' is missing. Set it in User Secrets or AUTH_DB_CONNECTION_STRING.");
        }

        services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IUserRepository, SqlUserRepository>();
        services.AddScoped<IUserActionLogService, DbUserActionLogService>();
        services.AddScoped<IRefreshTokenService, DbRefreshTokenService>();
        services.AddSingleton<ICompanyDirectoryService>(serviceProvider =>
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<SqlCompanyDirectoryService>();
            var companyDirectoryOptions = configuration
                .GetSection(CompanyDirectoryOptions.SectionName)
                .Get<CompanyDirectoryOptions>()
                ?? new CompanyDirectoryOptions();

            var externalConnectionString =
                Environment.GetEnvironmentVariable("LABORATORY_RASF_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(externalConnectionString))
            {
                externalConnectionString = configuration.GetConnectionString(companyDirectoryOptions.ConnectionStringName);
            }

            if (string.IsNullOrWhiteSpace(externalConnectionString))
            {
                logger.LogWarning(
                    "External company directory connection string is missing. Falling back to in-memory company directory.");
                return new InMemoryCompanyDirectoryService();
            }

            return new SqlCompanyDirectoryService(externalConnectionString, logger);
        });

        return services;
    }

    public static async Task ApplyAuthMigrationsIfEnabledAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<PersistenceOptions>>().Value;

        if (!options.UseSqlServer || !options.ApplyMigrationsOnStartup)
        {
            return;
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
