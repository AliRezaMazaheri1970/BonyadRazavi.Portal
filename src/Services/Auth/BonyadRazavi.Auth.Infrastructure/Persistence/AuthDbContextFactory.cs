using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AUTH_DB_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString =
                "Server=(localdb)\\MSSQLLocalDB;Database=BonyadRazaviAuth.DesignTime;Trusted_Connection=True;TrustServerCertificate=True";
        }

        var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new AuthDbContext(optionsBuilder.Options);
    }
}
