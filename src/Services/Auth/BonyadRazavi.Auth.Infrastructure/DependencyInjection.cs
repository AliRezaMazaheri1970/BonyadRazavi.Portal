using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PersistenceOptions>(configuration.GetSection(PersistenceOptions.SectionName));
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        var persistenceOptions = configuration.GetSection(PersistenceOptions.SectionName).Get<PersistenceOptions>()
            ?? new PersistenceOptions();

        if (!persistenceOptions.UseSqlServer)
        {
            services.AddSingleton<IUserRepository, InMemoryUserRepository>();
            services.AddSingleton<IUserActionLogService, NoOpUserActionLogService>();
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
