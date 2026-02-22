using Microsoft.EntityFrameworkCore;

namespace BonyadRazavi.Auth.Infrastructure.Persistence;

internal sealed class LaboratoryRasfReadDbContext : DbContext
{
    public LaboratoryRasfReadDbContext(DbContextOptions<LaboratoryRasfReadDbContext> options)
        : base(options)
    {
    }

    public DbSet<MasterBillRow> MasterBills => Set<MasterBillRow>();
    public DbSet<ContractsBaseRow> ContractsBase => Set<ContractsBaseRow>();
    public DbSet<ContractsFinancialRow> ContractsFinancial => Set<ContractsFinancialRow>();
    public DbSet<CompanyAgencyRow> CompaniesAgency => Set<CompanyAgencyRow>();
    public DbSet<ContractsRemindViewRow> ContractsRemindView => Set<ContractsRemindViewRow>();
    public DbSet<ReceiptAmountMasterRow> ReceiptAmountMaster => Set<ReceiptAmountMasterRow>();
    public DbSet<ReceiptAmountDetailRow> ReceiptAmountDetail => Set<ReceiptAmountDetailRow>();
    public DbSet<ReceiptsOfFundsViewRow> ReceiptsOfFundsView => Set<ReceiptsOfFundsViewRow>();
    public DbSet<PaymentAmountMasterRow> PaymentAmountMaster => Set<PaymentAmountMasterRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MasterBillRow>(entity =>
        {
            entity.ToTable("MasterBills", "dbo");
            entity.HasNoKey();
            entity.Property(item => item.MasterBillsCode).HasColumnName("MasterBillsCode");
            entity.Property(item => item.ContractCode).HasColumnName("ContractCode");
            entity.Property(item => item.BillNo).HasColumnName("BillNo");
            entity.Property(item => item.BillDate).HasColumnName("BillDate");
            entity.Property(item => item.IsVoid).HasColumnName("IsVoid");
            entity.Property(item => item.InformalFactor).HasColumnName("InformalFactor");
        });

        modelBuilder.Entity<ContractsBaseRow>(entity =>
        {
            entity.ToTable("Contracts_Base", "dbo");
            entity.HasNoKey();
            entity.Property(item => item.ContractsCode).HasColumnName("ContractsCode");
            entity.Property(item => item.CompanyInvoice).HasColumnName("Company_Invoice");
            entity.Property(item => item.ContractNo).HasColumnName("ContractNo");
            entity.Property(item => item.OfficesCode).HasColumnName("OfficesCode");
        });

        modelBuilder.Entity<ContractsFinancialRow>(entity =>
        {
            entity.ToTable("Contracts_Finacial", "dbo");
            entity.HasNoKey();
            entity.Property(item => item.ContractCode).HasColumnName("ContractCode");
            entity.Property(item => item.FinacialSupport).HasColumnName("FinacialSupport");
        });

        modelBuilder.Entity<CompanyAgencyRow>(entity =>
        {
            entity.ToTable("Companies_Agency", "dbo");
            entity.HasNoKey();
            entity.Property(item => item.AgencyCode).HasColumnName("AgencyCode");
            entity.Property(item => item.AgencyName).HasColumnName("AgencyName");
        });

        modelBuilder.Entity<ContractsRemindViewRow>(entity =>
        {
            entity.ToTable("Contracts_Remind_View", "dbo");
            entity.HasNoKey();
            entity.Property(item => item.ContractsCode).HasColumnName("ContractsCode");
            entity.Property(item => item.Debtor).HasColumnName("Debtor");
        });

        modelBuilder.Entity<ReceiptAmountMasterRow>(entity =>
        {
            entity.ToTable("ReceiptAmount_Master", "dbo");
            entity.HasNoKey();
            entity.Property(item => item.ReceiptAmountMasterCode).HasColumnName("ReceiptAmountMasterCode");
            entity.Property(item => item.CompaniesCode).HasColumnName("CompaniesCode");
            entity.Property(item => item.ReceiptNo).HasColumnName("ReceiptNo");
            entity.Property(item => item.ReceiptDate).HasColumnName("ReceiptDate");
            entity.Property(item => item.Amount).HasColumnName("Amount");
            entity.Property(item => item.HowToPay).HasColumnName("HowToPay");
            entity.Property(item => item.IsVoid).HasColumnName("Void");
            entity.Property(item => item.IsInformalReceipt).HasColumnName("InformalReceipt");
        });

        modelBuilder.Entity<ReceiptAmountDetailRow>(entity =>
        {
            entity.ToTable("ReceiptAmount_Detail", "dbo");
            entity.HasNoKey();
            entity.Property(item => item.ReceiptMasterCode).HasColumnName("ReceiptMasterCode");
            entity.Property(item => item.ParentReceipt).HasColumnName("ParentReceipt");
        });

        modelBuilder.Entity<ReceiptsOfFundsViewRow>(entity =>
        {
            entity.ToTable("Receiptoffunds_View", "dbo");
            entity.HasNoKey();
            entity.Property(item => item.ReceiptMasterCode).HasColumnName("ReceiptMasterCode");
            entity.Property(item => item.ContractNo).HasColumnName("ContractNo");
            entity.Property(item => item.Amount).HasColumnName("Amount");
        });

        modelBuilder.Entity<PaymentAmountMasterRow>(entity =>
        {
            entity.ToTable("PaymentAmount_Master", "dbo");
            entity.HasNoKey();
            entity.Property(item => item.CompaniesCode).HasColumnName("CompaniesCode");
            entity.Property(item => item.PaymentNo).HasColumnName("PaymentNo");
            entity.Property(item => item.PaymentDate).HasColumnName("PaymentDate");
            entity.Property(item => item.Amount).HasColumnName("Amount");
            entity.Property(item => item.IsVoid).HasColumnName("Void");
            entity.Property(item => item.IsInformalPayments).HasColumnName("InformalPayments");
        });
    }
}

internal sealed class MasterBillRow
{
    public Guid MasterBillsCode { get; set; }
    public Guid? ContractCode { get; set; }
    public int? BillNo { get; set; }
    public DateTime? BillDate { get; set; }
    public byte? IsVoid { get; set; }
    public bool? InformalFactor { get; set; }
}

internal sealed class ContractsBaseRow
{
    public Guid ContractsCode { get; set; }
    public Guid? CompanyInvoice { get; set; }
    public int? ContractNo { get; set; }
    public Guid? OfficesCode { get; set; }
}

internal sealed class ContractsFinancialRow
{
    public Guid ContractCode { get; set; }
    public bool? FinacialSupport { get; set; }
}

internal sealed class CompanyAgencyRow
{
    public Guid AgencyCode { get; set; }
    public string? AgencyName { get; set; }
}

internal sealed class ContractsRemindViewRow
{
    public Guid ContractsCode { get; set; }
    public long? Debtor { get; set; }
}

internal sealed class ReceiptAmountMasterRow
{
    public Guid ReceiptAmountMasterCode { get; set; }
    public Guid? CompaniesCode { get; set; }
    public int? ReceiptNo { get; set; }
    public DateTime? ReceiptDate { get; set; }
    public long? Amount { get; set; }
    public int? HowToPay { get; set; }
    public byte? IsVoid { get; set; }
    public bool? IsInformalReceipt { get; set; }
}

internal sealed class ReceiptAmountDetailRow
{
    public Guid? ReceiptMasterCode { get; set; }
    public Guid? ParentReceipt { get; set; }
}

internal sealed class ReceiptsOfFundsViewRow
{
    public Guid ReceiptMasterCode { get; set; }
    public int? ContractNo { get; set; }
    public long? Amount { get; set; }
}

internal sealed class PaymentAmountMasterRow
{
    public Guid? CompaniesCode { get; set; }
    public int? PaymentNo { get; set; }
    public DateTime? PaymentDate { get; set; }
    public long? Amount { get; set; }
    public byte? IsVoid { get; set; }
    public bool? IsInformalPayments { get; set; }
}
