namespace BonyadRazavi.Auth.Application.Models;

public sealed record CompanyInvoiceDocument(
    string FileName,
    byte[] Content,
    string ContentType);
