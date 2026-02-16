namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public bool UseSqlServer { get; set; }
    public string ConnectionStringName { get; set; } = "AuthDb";
    public bool ApplyMigrationsOnStartup { get; set; }
}
