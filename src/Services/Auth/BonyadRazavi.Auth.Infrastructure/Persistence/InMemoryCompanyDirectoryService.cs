using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Models;

namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public sealed class InMemoryCompanyDirectoryService : ICompanyDirectoryService
{
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
