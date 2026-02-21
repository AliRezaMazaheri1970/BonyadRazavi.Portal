using BonyadRazavi.Auth.Application.Models;
using BonyadRazavi.Shared.Contracts.Companies;

namespace BonyadRazavi.Auth.Application.Abstractions;

public interface ICompanyInvoiceReportService
{
    Task<IReadOnlyCollection<CompanyInvoiceDto>> GetInvoicesByCompanyAsync(
        Guid companyCode,
        CancellationToken cancellationToken = default);

    Task<CompanyInvoiceDocument?> GetInvoicePdfAsync(
        Guid companyCode,
        Guid masterBillCode,
        CancellationToken cancellationToken = default);
}
