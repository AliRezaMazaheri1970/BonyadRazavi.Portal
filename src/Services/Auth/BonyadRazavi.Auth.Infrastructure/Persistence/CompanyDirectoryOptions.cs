namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public sealed class CompanyDirectoryOptions
{
    public const string SectionName = "CompanyDirectory";

    public string ConnectionStringName { get; set; } = "LaboratoryRASF";
}
