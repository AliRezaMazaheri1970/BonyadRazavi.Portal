using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Models;

namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public sealed class InMemoryCompanyDirectoryService : ICompanyDirectoryService
{
    private static readonly CompanyDirectoryEntry[] SeedEntries =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Sample Company 1"),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Sample Company 2")
    ];

    public Task<CompanyDirectoryEntry?> FindByCodeAsync(
        Guid companyCode,
        CancellationToken cancellationToken = default)
    {
        if (companyCode == Guid.Empty)
        {
            return Task.FromResult<CompanyDirectoryEntry?>(null);
        }

        return Task.FromResult<CompanyDirectoryEntry?>(
            new CompanyDirectoryEntry(companyCode, $"Company-{companyCode}"));
    }

    public Task<IReadOnlyCollection<CompanyDirectoryEntry>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<CompanyDirectoryEntry>>(SeedEntries);
    }

    public Task<IReadOnlyDictionary<Guid, string?>> GetNamesByCodesAsync(
        IReadOnlyCollection<Guid> companyCodes,
        CancellationToken cancellationToken = default)
    {
        var result = companyCodes
            .Where(companyCode => companyCode != Guid.Empty)
            .Distinct()
            .ToDictionary(
                companyCode => companyCode,
                companyCode => (string?)$"Company-{companyCode}");

        return Task.FromResult<IReadOnlyDictionary<Guid, string?>>(result);
    }
}
