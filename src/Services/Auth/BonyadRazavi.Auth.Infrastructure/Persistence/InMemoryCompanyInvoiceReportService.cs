using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Models;
using BonyadRazavi.Shared.Contracts.Companies;

namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public sealed class InMemoryCompanyInvoiceReportService : ICompanyInvoiceReportService
{
    public Task<IReadOnlyCollection<CompanyInvoiceDto>> GetInvoicesByCompanyAsync(
        Guid companyCode,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<CompanyInvoiceDto>>([]);
    }

    public Task<CompanyInvoiceDocument?> GetInvoicePdfAsync(
        Guid companyCode,
        Guid masterBillCode,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<CompanyInvoiceDocument?>(null);
    }
}
