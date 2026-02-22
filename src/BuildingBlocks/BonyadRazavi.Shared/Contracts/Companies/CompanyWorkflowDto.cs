namespace BonyadRazavi.Shared.Contracts.Companies;

public sealed class CompanyWorkflowDto
{
    public int BillNo { get; set; }
    public DateTime BillDate { get; set; }
    public long Debtor { get; set; }
    public long Creditor { get; set; }
    public long Remind { get; set; }
    public long Reminding { get; set; }
    public string ContractsNo { get; set; } = string.Empty;
    public string AgencyName { get; set; } = string.Empty;
    public string TypeInvoice { get; set; } = string.Empty;
}
