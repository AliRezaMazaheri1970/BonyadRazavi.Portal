using BonyadRazavi.Auth.Application.Models;

namespace BonyadRazavi.Auth.Application.Abstractions;

public interface ICompanyDirectoryService
{
    Task<CompanyDirectoryEntry?> FindByCodeAsync(
        Guid companyCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CompanyDirectoryEntry>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string?>> GetNamesByCodesAsync(
        IReadOnlyCollection<Guid> companyCodes,
        CancellationToken cancellationToken = default);
}
