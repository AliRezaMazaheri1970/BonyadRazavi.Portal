namespace BonyadRazavi.Shared.Contracts.Companies;

public sealed class CompanyInvoiceDto
{
    public Guid MasterBillCode { get; set; }
    public string BillNo { get; set; } = string.Empty;
    public string ContractNo { get; set; } = string.Empty;
    public DateTime BillDate { get; set; }
    public decimal TotalPrice { get; set; }
}
