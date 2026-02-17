using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public sealed class SqlCompanyDirectoryService : ICompanyDirectoryService
{
    private readonly string _connectionString;
    private readonly ILogger<SqlCompanyDirectoryService> _logger;

    public SqlCompanyDirectoryService(
        string connectionString,
        ILogger<SqlCompanyDirectoryService> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<CompanyDirectoryEntry?> FindByCodeAsync(
        Guid companyCode,
        CancellationToken cancellationToken = default)
    {
        if (companyCode == Guid.Empty)
        {
            return null;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TOP (1) CompaniesCode, CompanyName
                FROM dbo.Companies_Base
                WHERE CompaniesCode = @CompanyCode
                """;
            command.Parameters.Add(new SqlParameter("@CompanyCode", companyCode));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var code = reader.GetGuid(0);
            var name = reader.IsDBNull(1) ? null : reader.GetString(1);
            return new CompanyDirectoryEntry(code, name);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to load company metadata for CompanyCode {CompanyCode} from LaboratoryRASF.",
                companyCode);
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<Guid, string?>> GetNamesByCodesAsync(
        IReadOnlyCollection<Guid> companyCodes,
        CancellationToken cancellationToken = default)
    {
        var codes = companyCodes
            .Where(code => code != Guid.Empty)
            .Distinct()
            .ToArray();
        if (codes.Length == 0)
        {
            return new Dictionary<Guid, string?>();
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();

            var parameterNames = new List<string>(codes.Length);
            for (var index = 0; index < codes.Length; index++)
            {
                var parameterName = $"@p{index}";
                parameterNames.Add(parameterName);
                command.Parameters.Add(new SqlParameter(parameterName, codes[index]));
            }

            command.CommandText =
                $"SELECT CompaniesCode, CompanyName FROM dbo.Companies_Base WHERE CompaniesCode IN ({string.Join(", ", parameterNames)})";

            var result = new Dictionary<Guid, string?>(codes.Length);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var code = reader.GetGuid(0);
                var name = reader.IsDBNull(1) ? null : reader.GetString(1);
                result[code] = name;
            }

            return result;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to load company metadata list from LaboratoryRASF.");
            return new Dictionary<Guid, string?>();
        }
    }
}
