using System.Globalization;
using BonyadRazavi.Auth.Application.Abstractions;
using BonyadRazavi.Auth.Application.Models;
using BonyadRazavi.Shared.Contracts.Companies;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BonyadRazavi.Auth.Infrastructure.Persistence;

public sealed class SqlCompanyInvoiceReportService : ICompanyInvoiceReportService
{
    private static readonly CultureInfo PersianCulture = new("fa-IR");
    private static readonly DateTime WorkflowDefaultDate = new(1900, 1, 1);

    private readonly string _connectionString;
    private readonly ILogger<SqlCompanyInvoiceReportService> _logger;
    private readonly byte[]? _logoBytes;

    static SqlCompanyInvoiceReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public SqlCompanyInvoiceReportService(
        string connectionString,
        ILogger<SqlCompanyInvoiceReportService> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
        _logoBytes = ResolveLogoBytes();
    }

    private LaboratoryRasfReadDbContext CreateLaboratoryRasfReadDbContext()
    {
        var options = new DbContextOptionsBuilder<LaboratoryRasfReadDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        return new LaboratoryRasfReadDbContext(options);
    }

    public async Task<IReadOnlyCollection<CompanyInvoiceDto>> GetInvoicesByCompanyAsync(
        Guid companyCode,
        CancellationToken cancellationToken = default)
    {
        if (companyCode == Guid.Empty)
        {
            return [];
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    mb.MasterBillsCode,
                    mb.BillNo,
                    cb.ContractNo,
                    mb.BillDate,
                    mb.TotalPrice
                FROM dbo.MasterBills AS mb
                LEFT JOIN dbo.Contracts_Base AS cb ON cb.ContractsCode = mb.ContractCode
                WHERE
                    mb.IsVoid = 0
                    AND mb.InformalFactor = 0
                    AND cb.Void = 0
                    AND cb.Company_Invoice = @CompanyCode
                ORDER BY mb.BillDate DESC, mb.MasterBillsCode DESC
                """;
            command.Parameters.Add(new SqlParameter("@CompanyCode", companyCode));

            var result = new List<CompanyInvoiceDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var invoice = new CompanyInvoiceDto
                {
                    MasterBillCode = reader.GetGuid(0),
                    BillNo = reader.IsDBNull(1)
                        ? string.Empty
                        : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                    ContractNo = reader.IsDBNull(2)
                        ? string.Empty
                        : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                    BillDate = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                    TotalPrice = reader.IsDBNull(4)
                        ? 0
                        : Convert.ToDecimal(reader.GetValue(4), CultureInfo.InvariantCulture)
                };

                result.Add(invoice);
            }

            return result;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to load invoices for CompanyCode {CompanyCode} from LaboratoryRASF.",
                companyCode);
            return [];
        }
    }

    public async Task<IReadOnlyCollection<CompanyWorkflowDto>> GetWorkflowByCompanyAsync(
        Guid companyCode,
        CancellationToken cancellationToken = default)
    {
        if (companyCode == Guid.Empty)
        {
            return [];
        }

        try
        {
            await using var dbContext = CreateLaboratoryRasfReadDbContext();

            var invoiceBaseRows = await (
                    from mb in dbContext.MasterBills.AsNoTracking()
                    join cb in dbContext.ContractsBase.AsNoTracking() on mb.ContractCode equals cb.ContractsCode
                    join cf in dbContext.ContractsFinancial.AsNoTracking() on cb.ContractsCode equals cf.ContractCode
                    where mb.IsVoid == 0
                          && mb.InformalFactor == false
                          && cb.CompanyInvoice == companyCode
                          && cf.FinacialSupport == true
                    select new
                    {
                        ContractCode = cb.ContractsCode,
                        cb.ContractNo,
                        cb.OfficesCode,
                        mb.BillNo,
                        mb.BillDate
                    })
                .ToListAsync(cancellationToken);

            var contractCodes = invoiceBaseRows
                .Select(row => row.ContractCode)
                .Distinct()
                .ToArray();

            var officeCodes = invoiceBaseRows
                .Select(row => row.OfficesCode)
                .Where(code => code.HasValue)
                .Select(code => code!.Value)
                .Distinct()
                .ToArray();

            Dictionary<Guid, string> agencyByOfficeCode = [];
            if (officeCodes.Length > 0)
            {
                agencyByOfficeCode = await dbContext.CompaniesAgency
                    .AsNoTracking()
                    .Where(row => officeCodes.Contains(row.AgencyCode))
                    .GroupBy(row => row.AgencyCode)
                    .Select(group => new
                    {
                        OfficeCode = group.Key,
                        AgencyName = group.Select(item => item.AgencyName).FirstOrDefault()
                    })
                    .ToDictionaryAsync(
                        item => item.OfficeCode,
                        item => item.AgencyName ?? string.Empty,
                        cancellationToken);
            }

            Dictionary<Guid, long> debtorByContractCode = [];
            if (contractCodes.Length > 0)
            {
                debtorByContractCode = await dbContext.ContractsRemindView
                    .AsNoTracking()
                    .Where(row => contractCodes.Contains(row.ContractsCode))
                    .GroupBy(row => row.ContractsCode)
                    .Select(group => new
                    {
                        ContractCode = group.Key,
                        Debtor = group.Select(item => item.Debtor).FirstOrDefault()
                    })
                    .ToDictionaryAsync(
                        item => item.ContractCode,
                        item => item.Debtor ?? 0,
                        cancellationToken);
            }

            var invoiceRows = invoiceBaseRows.Select(row => new
            {
                row.BillNo,
                row.BillDate,
                Debtor = debtorByContractCode.TryGetValue(row.ContractCode, out var debtor) ? debtor : 0,
                row.ContractNo,
                AgencyName = row.OfficesCode.HasValue
                    && agencyByOfficeCode.TryGetValue(row.OfficesCode.Value, out var agencyName)
                    ? agencyName
                    : string.Empty
            }).ToList();

            var receiptRows = await (
                    from rm in dbContext.ReceiptAmountMaster.AsNoTracking()
                    where rm.IsVoid == 0
                          && rm.IsInformalReceipt == false
                          && rm.CompaniesCode == companyCode
                          && dbContext.ReceiptAmountDetail.AsNoTracking().Any(rd =>
                              rd.ReceiptMasterCode == rm.ReceiptAmountMasterCode
                              && rd.ParentReceipt == null)
                    select new
                    {
                        rm.ReceiptAmountMasterCode,
                        rm.ReceiptNo,
                        rm.ReceiptDate,
                        rm.Amount,
                        rm.HowToPay
                    })
                .Distinct()
                .ToListAsync(cancellationToken);

            Dictionary<Guid, string> receiptContractsByMasterCode = [];
            var receiptMasterCodes = receiptRows
                .Select(row => row.ReceiptAmountMasterCode)
                .Distinct()
                .ToArray();
            if (receiptMasterCodes.Length > 0)
            {
                var receiptContracts = await (
                        from rv in dbContext.ReceiptsOfFundsView.AsNoTracking()
                        where receiptMasterCodes.Contains(rv.ReceiptMasterCode)
                        group rv by new { rv.ReceiptMasterCode, rv.ContractNo } into grouped
                        where grouped.Sum(item => item.Amount ?? 0) > 0
                        select new
                        {
                            grouped.Key.ReceiptMasterCode,
                            grouped.Key.ContractNo
                        })
                    .ToListAsync(cancellationToken);

                receiptContractsByMasterCode = receiptContracts
                    .Where(item => item.ContractNo.HasValue)
                    .GroupBy(item => item.ReceiptMasterCode)
                    .ToDictionary(
                        group => group.Key,
                        group => string.Join(",", group
                            .Select(item => item.ContractNo!.Value.ToString(CultureInfo.InvariantCulture))
                            .Distinct(StringComparer.Ordinal)));
            }

            var paymentRows = await (
                    from pm in dbContext.PaymentAmountMaster.AsNoTracking()
                    where pm.CompaniesCode == companyCode
                          && pm.IsVoid == 0
                          && pm.IsInformalPayments == false
                    select new
                    {
                        pm.PaymentNo,
                        pm.PaymentDate,
                        pm.Amount
                    })
                .ToListAsync(cancellationToken);

            var rawRows = new List<CompanyWorkflowRawRow>(invoiceRows.Count + receiptRows.Count + paymentRows.Count);

            rawRows.AddRange(invoiceRows.Select(row => new CompanyWorkflowRawRow(
                row.BillNo ?? 0,
                row.BillDate ?? WorkflowDefaultDate,
                row.Debtor,
                0,
                row.ContractNo.HasValue ? row.ContractNo.Value.ToString(CultureInfo.InvariantCulture) : "-",
                row.AgencyName ?? string.Empty,
                "صورت حساب")));

            rawRows.AddRange(receiptRows.Select(row =>
            {
                var contractsNo = receiptContractsByMasterCode.TryGetValue(row.ReceiptAmountMasterCode, out var value)
                    ? value
                    : "-";
                if (string.IsNullOrWhiteSpace(contractsNo))
                {
                    contractsNo = "-";
                }

                return new CompanyWorkflowRawRow(
                    row.ReceiptNo ?? 0,
                    row.ReceiptDate ?? WorkflowDefaultDate,
                    0,
                    row.Amount ?? 0,
                    contractsNo,
                    string.Empty,
                    ResolveReceiptTypeInvoice(row.HowToPay));
            }));

            rawRows.AddRange(paymentRows.Select(row => new CompanyWorkflowRawRow(
                row.PaymentNo ?? 0,
                row.PaymentDate ?? WorkflowDefaultDate,
                row.Amount ?? 0,
                0,
                "0",
                string.Empty,
                "پرداختی")));

            rawRows = rawRows
                .OrderBy(row => row.BillDate)
                .ThenBy(row => row.BillNo)
                .ToList();

            var result = new List<CompanyWorkflowDto>(rawRows.Count);
            long runningBalance = 0;
            foreach (var row in rawRows)
            {
                var remind = row.Debtor - row.Creditor;
                runningBalance += remind;

                result.Add(new CompanyWorkflowDto
                {
                    BillNo = row.BillNo,
                    BillDate = row.BillDate,
                    Debtor = row.Debtor,
                    Creditor = row.Creditor,
                    Remind = remind,
                    Reminding = runningBalance,
                    ContractsNo = row.ContractsNo,
                    AgencyName = row.AgencyName,
                    TypeInvoice = row.TypeInvoice
                });
            }

            return result;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to load workflow rows for CompanyCode {CompanyCode} from LaboratoryRASF.",
                companyCode);
            return [];
        }
    }

    public async Task<CompanyInvoiceDocument?> GetInvoicePdfAsync(
        Guid companyCode,
        Guid masterBillCode,
        CancellationToken cancellationToken = default)
    {
        if (companyCode == Guid.Empty || masterBillCode == Guid.Empty)
        {
            return null;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var metadata = await ResolveInvoiceMetadataAsync(
                connection,
                companyCode,
                masterBillCode,
                cancellationToken);
            if (metadata is null)
            {
                return null;
            }

            var contract = await ResolveContractInfoAsync(
                connection,
                metadata.Value.ContractCode,
                masterBillCode,
                cancellationToken);
            if (contract is null)
            {
                return null;
            }

            var items = await ResolveItemsAsync(connection, masterBillCode, cancellationToken);
            if (items.Count == 0)
            {
                items =
                [
                    new InvoiceLineItem(
                        "صورت حساب",
                        1,
                        metadata.Value.TotalPrice,
                        metadata.Value.TotalPrice,
                        0,
                        0,
                        int.TryParse(metadata.Value.BillNo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var billNumber)
                            ? billNumber
                            : 0)
                ];
            }

            var taxRate = await ResolveTaxRateAsync(connection, cancellationToken);
            var payload = new InvoicePdfPayload(metadata.Value, contract.Value, items, taxRate);
            var fileBytes = BuildPdf(payload);
            var fileName = BuildPdfFileName(metadata.Value.BillNo, masterBillCode);
            return new CompanyInvoiceDocument(fileName, fileBytes, "application/pdf");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to generate invoice PDF for CompanyCode {CompanyCode} and MasterBillCode {MasterBillCode}.",
                companyCode,
                masterBillCode);
            return null;
        }
    }

    private static async Task<InvoiceMetadata?> ResolveInvoiceMetadataAsync(
        SqlConnection connection,
        Guid companyCode,
        Guid masterBillCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP(1)
                mb.ContractCode,
                mb.BillNo,
                mb.BillDate,
                mb.TotalPrice,
                cb.ContractNo
            FROM dbo.MasterBills AS mb
            LEFT JOIN dbo.Contracts_Base AS cb ON cb.ContractsCode = mb.ContractCode
            WHERE
                mb.MasterBillsCode = @MasterBillCode
                AND mb.IsVoid = 0
                AND mb.InformalFactor = 0
                AND cb.Void = 0
                AND cb.Company_Invoice = @CompanyCode
            """;
        command.Parameters.Add(new SqlParameter("@MasterBillCode", masterBillCode));
        command.Parameters.Add(new SqlParameter("@CompanyCode", companyCode));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0))
        {
            return null;
        }

        var contractCode = reader.GetGuid(0);
        var billNo = reader.IsDBNull(1)
            ? string.Empty
            : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty;
        var billDate = reader.IsDBNull(2) ? DateTime.MinValue : reader.GetDateTime(2);
        var totalPrice = reader.IsDBNull(3)
            ? 0
            : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture);
        var contractNo = reader.IsDBNull(4)
            ? string.Empty
            : Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty;

        return new InvoiceMetadata(contractCode, billNo, billDate, totalPrice, contractNo);
    }

    private static async Task<InvoiceContractInfo?> ResolveContractInfoAsync(
        SqlConnection connection,
        Guid contractCode,
        Guid masterBillCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP(1)
                cb.CompanyName,
                cb.CmpanyAddress,
                cb.EconomicCode,
                cb.SetNum,
                dbo.DateConvertor(cn.LetterDate) AS LetterDate,
                cn.LetterNo,
                cn.ContractNo,
                cb.PostCode,
                dbo.DateConvertorWithTime(mb.BillDate) AS BillDateDisplay,
                cb.FinancialCode1 AS FinancialCode,
                cb.NationalCode,
                cb.NationalArgument,
                ISNULL(cn.ExpertCompany, N'') AS ExpertCompany,
                ISNULL(agency.AgencyName, N'') AS AgencyName,
                ISNULL(mobile.MobileNum, N'') AS MobileNum,
                CASE
                    WHEN coop.CooperationCode IN
                    (
                        'BF6434F1-807B-40DB-8B96-3EA4A65671F0',
                        'D04DDB41-9B90-4D58-ABE4-405705523A48',
                        '969B271E-0392-4A25-99C0-486B3115F336',
                        '8F655027-8187-412E-9CED-49433CA62A3D',
                        'D7AD3470-063B-4472-A859-892DD77891BB'
                    )
                    THEN N'نقدی'
                    ELSE coop.CooperationName
                END AS CooperationName
            FROM dbo.Contracts_Base AS cn
            LEFT JOIN dbo.Companies_Base AS cb ON cb.CompaniesCode = cn.Company_Invoice
            LEFT JOIN dbo.Companies_Cooperation AS coop ON cb.CooperationCode = coop.CooperationCode
            LEFT JOIN dbo.Companies_Agency AS agency ON agency.AgencyCode = cn.OfficesCode
            LEFT JOIN dbo.MasterBills AS mb ON mb.ContractCode = cn.ContractsCode
            OUTER APPLY
            (
                SELECT TOP(1) m.MobileNum
                FROM dbo.Companies_MobileNum AS m
                WHERE m.CompanyCode = cn.Company_Invoice
                ORDER BY m.MobileNum
            ) AS mobile
            WHERE
                cn.ContractsCode = @ContractCode
                AND mb.MasterBillsCode = @MasterBillCode
                AND mb.IsVoid = 0
            """;
        command.Parameters.Add(new SqlParameter("@ContractCode", contractCode));
        command.Parameters.Add(new SqlParameter("@MasterBillCode", masterBillCode));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new InvoiceContractInfo(
            ReadString(reader, 0),
            ReadString(reader, 1),
            ReadString(reader, 2),
            ReadString(reader, 3),
            ReadString(reader, 4),
            ReadString(reader, 5),
            ReadString(reader, 6),
            ReadString(reader, 7),
            ReadString(reader, 8),
            ReadString(reader, 9),
            ReadString(reader, 10),
            ReadString(reader, 11),
            ReadString(reader, 12),
            ReadString(reader, 13),
            ReadString(reader, 14),
            ReadString(reader, 15));
    }

    private static async Task<IReadOnlyList<InvoiceLineItem>> ResolveItemsAsync(
        SqlConnection connection,
        Guid masterBillCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                ISNULL(mt.MasterName, N'-') AS MasterName,
                db.NumberOfTests,
                CAST(db.UnitPrice AS bigint) AS InvoicePrice,
                db.NumberOfTests * CAST(db.UnitPrice AS bigint) AS TotalPrice,
                CAST(
                    (db.NumberOfTests * CAST(db.UnitPrice AS bigint)) * (db.DisCount / 100.0)
                    AS int
                ) AS DisCount,
                CAST(
                    ROUND(
                        (
                            (db.NumberOfTests * CAST(db.UnitPrice AS bigint))
                            - (db.NumberOfTests * CAST(db.UnitPrice AS bigint)) * (db.DisCount / 100.0)
                        ) * (db.Tax / 100.0),
                        0
                    )
                    AS int
                ) AS TaxAmount,
                mb.BillNo AS BillNumber
            FROM dbo.DetailBills AS db
            INNER JOIN dbo.MasterBills AS mb ON mb.MasterBillsCode = db.MasterBillsCode
            LEFT JOIN dbo.MasterTest AS mt ON mt.MasterTestCode = db.MasterTestCode
            WHERE db.MasterBillsCode = @MasterBillsCode
              AND db.UnitPrice > 0
            ORDER BY mt.MasterName
            """;
        command.Parameters.Add(new SqlParameter("@MasterBillsCode", masterBillCode));

        var result = new List<InvoiceLineItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new InvoiceLineItem(
                ReadString(reader, 0),
                ReadInt32(reader, 1),
                ReadDecimal(reader, 2),
                ReadDecimal(reader, 3),
                ReadDecimal(reader, 4),
                ReadDecimal(reader, 5),
                ReadInt32(reader, 6)));
        }

        return result;
    }

    private static async Task<int> ResolveTaxRateAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "exec CalculateTax_Select";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return 0;
        }

        var taxOrdinal = reader.GetOrdinal("Tax");
        var taxValue = ReadInt32(reader, taxOrdinal);
        return taxValue >= 0 ? taxValue : 0;
    }

    private byte[] BuildPdf(InvoicePdfPayload payload)
    {
        var seller = SellerProfile.Default;
        var invoiceDate = payload.Contract.BillDateDisplay;
        if (string.IsNullOrWhiteSpace(invoiceDate) && payload.Metadata.BillDate != DateTime.MinValue)
        {
            invoiceDate = payload.Metadata.BillDate.ToString("yyyy/MM/dd HH:mm", PersianCulture);
        }

        var subTotal = payload.Items.Sum(item => item.TotalPrice);
        var discount = payload.Items.Sum(item => item.Discount);
        var netTotal = subTotal - discount;
        var tax = payload.Items.Sum(item => item.Tax);
        var grandTotal = netTotal + tax;
        var agencyName = ResolveAgencyName(payload.Contract.AgencyName);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(style => style.FontFamily("Tahoma").FontSize(9));

                page.Header().Element(OuterBorder).Padding(4).Row(row =>
                {
                    row.ConstantItem(220).AlignLeft().Column(meta =>
                    {
                        meta.Item().AlignLeft().Text($"تاریخ صدور: {ToPersianNumber(invoiceDate)}").SemiBold();
                        meta.Item().AlignLeft().Text($"شماره صورت حساب: {ToPersianNumber(payload.Metadata.BillNo)}").SemiBold();
                    });

                    row.RelativeItem().AlignCenter().AlignMiddle().Text("صورت حساب فروش کالا و خدمات")
                        .SemiBold()
                        .FontSize(16);

                    row.ConstantItem(70).Height(56).AlignCenter().AlignMiddle().Element(container =>
                    {
                        if (_logoBytes is { Length: > 0 })
                        {
                            container.Image(_logoBytes).FitArea();
                        }
                        else
                        {
                            container.Text(string.Empty);
                        }
                    });
                });

                page.Content().PaddingTop(6).Column(column =>
                {
                    column.Spacing(4);

                    column.Item().Element(OuterBorder).Table(section =>
                    {
                        section.ColumnsDefinition(columns =>
                        {
                            for (var i = 0; i < 12; i++)
                            {
                                columns.RelativeColumn();
                            }
                        });

                        section.Cell().ColumnSpan(12).Element(SectionGridTitleCell).Text("مشخصات فروشنده").SemiBold().FontSize(11);
                        section.Cell().ColumnSpan(12).Element(SectionGridCell).AlignRight().Text($":شخص حقیقی / حقوقی   {seller.Name}");
                        section.Cell().ColumnSpan(5).Element(SectionGridCell).AlignRight().Text($"شماره اقتصادی: {ToPersianNumber(seller.EconomicCode)}");
                        section.Cell().ColumnSpan(4).Element(SectionGridCell).AlignRight().Text($"شماره ثبت / شماره ملی: {ToPersianNumber(seller.RegistrationCode)}");
                        section.Cell().ColumnSpan(3).Element(SectionGridCell).AlignRight().Text($"شناسه ملی: {ToPersianNumber(seller.NationalId)}");
                        section.Cell().ColumnSpan(12).Element(SectionGridCell).AlignRight().Text($"نشانی کامل: {seller.Address}");
                        section.Cell().ColumnSpan(5).Element(SectionGridCell).AlignRight().Text($"کد پستی 10 رقمی: {ToPersianNumber(seller.PostCode)}");
                        section.Cell().ColumnSpan(7).Element(SectionGridCell).AlignRight().Text($"شماره تلفن / نمابر: {ToPersianNumber(seller.Phone)}");

                        section.Cell().ColumnSpan(12).Element(SectionGridTitleCell).Text("مشخصات خریدار").SemiBold().FontSize(11);
                        section.Cell().ColumnSpan(12).Element(SectionGridCell).AlignRight().Text($":شخص حقیقی / حقوقی   {payload.Contract.CompanyName}");
                        section.Cell().ColumnSpan(4).Element(SectionGridCell).AlignRight().Text($"شماره اقتصادی: {ToPersianNumber(payload.Contract.EconomicCode)}");
                        section.Cell().ColumnSpan(4).Element(SectionGridCell).AlignRight().Text($"شناسه ملی: {ToPersianNumber(payload.Contract.NationalArgument)}");
                        section.Cell().ColumnSpan(4).Element(SectionGridCell).AlignRight().Text($"شماره ثبت: {ToPersianNumber(payload.Contract.SetNum)}");
                        section.Cell().ColumnSpan(7).Element(SectionGridCell).AlignRight().Text($"نشانی کامل: {payload.Contract.Address}");
                        section.Cell().ColumnSpan(2).Element(SectionGridCell).AlignRight().Text($"کارشناس: {payload.Contract.ExpertCompany}");
                        section.Cell().ColumnSpan(3).Element(SectionGridCell).AlignRight().Text($"شماره مشتری: {ToPersianNumber(payload.Contract.CustomerNumber)}");
                        section.Cell().ColumnSpan(4).Element(SectionGridCell).AlignRight().Text($"کد پستی 10 رقمی: {ToPersianNumber(payload.Contract.PostCode)}");
                        section.Cell().ColumnSpan(4).Element(SectionGridCell).AlignRight().Text($"عطف به شماره نامه: {ToPersianNumber(payload.Contract.LetterNo)}");
                        section.Cell().ColumnSpan(2).Element(SectionGridCell).AlignRight().Text($"مورخ: {ToPersianNumber(payload.Contract.LetterDate)}");
                        section.Cell().ColumnSpan(2).Element(SectionGridCell).AlignRight().Text($"شماره پیگیری: {ToPersianNumber(payload.Contract.ContractNo)}");
                    });

                    column.Item().PaddingTop(2).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(28); // مبلغ کل بعلاوه عوارض و مالیات
                            columns.RelativeColumn(28); // مالیات
                            columns.RelativeColumn(24); // مبلغ پس از تخفیف
                            columns.RelativeColumn(16); // تخفیف
                            columns.RelativeColumn(28); // مبلغ کل
                            columns.RelativeColumn(26); // مبلغ واحد
                            columns.RelativeColumn(18); // واحد
                            columns.RelativeColumn(16); // تعداد
                            columns.RelativeColumn(60); // شرح
                            columns.RelativeColumn(16); // کد کالا
                            columns.RelativeColumn(16); // ردیف
                        });

                        table.Header(header =>
                        {
                            header.Cell().ColumnSpan(11).Element(TableHeaderCell).Text("مشخصات کالا یا خدمات مورد معامله").SemiBold().FontSize(11);
                            header.Cell().Element(TableHeaderCell).Text("مبلغ کل بعلاوه عوارض و\nمالیات ارزش افزوده (ریال)");
                            header.Cell().Element(TableHeaderCell).Text($"0.0{ToPersianNumber(payload.TaxRate.ToString(CultureInfo.InvariantCulture))} عوارض و مالیات\nارزش افزوده (ریال)");
                            header.Cell().Element(TableHeaderCell).Text("مبلغ کل پس از\nتخفیف (ریال)");
                            header.Cell().Element(TableHeaderCell).Text("مبلغ تخفیف\n(ریال)");
                            header.Cell().Element(TableHeaderCell).Text("مبلغ کل\n(ریال)");
                            header.Cell().Element(TableHeaderCell).Text("مبلغ واحد\n(ریال)");
                            header.Cell().Element(TableHeaderCell).Text("واحد\nاندازه گیری");
                            header.Cell().Element(TableHeaderCell).Text("مقدار/واحد");
                            header.Cell().Element(TableHeaderCell).Text("شرح کالا یا خدمت");
                            header.Cell().Element(TableHeaderCell).Text("کد کالا");
                            header.Cell().Element(TableHeaderCell).Text("ردیف");
                        });

                        var rowIndex = 1;
                        foreach (var item in payload.Items)
                        {
                            var afterDiscount = item.TotalPrice - item.Discount;
                            var finalAmount = afterDiscount + item.Tax;

                            table.Cell().Element(TableBodyCell).AlignCenter().Text(FormatAmount(finalAmount));
                            table.Cell().Element(TableBodyCell).AlignCenter().Text(FormatAmount(item.Tax));
                            table.Cell().Element(TableBodyCell).AlignCenter().Text(FormatAmount(afterDiscount));
                            table.Cell().Element(TableBodyCell).AlignCenter().Text(FormatAmount(item.Discount));
                            table.Cell().Element(TableBodyCell).AlignCenter().Text(FormatAmount(item.TotalPrice));
                            table.Cell().Element(TableBodyCell).AlignCenter().Text(FormatAmount(item.InvoicePrice));
                            table.Cell().Element(TableBodyCell).AlignCenter().Text("عدد");
                            table.Cell().Element(TableBodyCell).AlignCenter().Text(ToPersianNumber(item.NumberOfTests.ToString(CultureInfo.InvariantCulture)));
                            table.Cell().Element(TableBodyCell).AlignRight().Text(item.MasterName);
                            table.Cell().Element(TableBodyCell).AlignCenter().Text(string.Empty);
                            table.Cell().Element(TableBodyCell).AlignCenter().Text(ToPersianNumber(rowIndex.ToString(CultureInfo.InvariantCulture)));
                            rowIndex++;
                        }

                        table.Cell().Element(TableBodyCell).AlignCenter().Text(FormatAmount(grandTotal)).SemiBold();
                        table.Cell().Element(TableBodyCell).AlignCenter().Text(FormatAmount(tax)).SemiBold();
                        table.Cell().Element(TableBodyCell).AlignCenter().Text(FormatAmount(netTotal)).SemiBold();
                        table.Cell().Element(TableBodyCell).AlignCenter().Text(FormatAmount(discount)).SemiBold();
                        table.Cell().Element(TableBodyCell).AlignCenter().Text(FormatAmount(subTotal)).SemiBold();
                        table.Cell().ColumnSpan(6).Element(TableBodyCell).AlignCenter().Text("جمع کل").SemiBold().FontSize(11);
                    });

                    column.Item().PaddingTop(2).AlignRight().Text($":شرایط و نحوه فروش   {payload.Contract.CooperationName}").SemiBold();
                    column.Item().AlignRight().Text($":محل ارسال   {agencyName}").SemiBold();
                    column.Item().AlignRight().Text($":کد تفضیلی   {ToPersianNumber(payload.Contract.FinancialCode)}").SemiBold();
                    column.Item().AlignRight().Text(".توجه: ارائه صورت حساب الزاما به منزله پرداخت وجه نمی باشد");
                    column.Item().AlignRight().Text(
                        "خواهشمند است هزینه انجام آزمون ها صرفا از طریق صدور چک و یا واریز به حساب بانک پارسیان به نام بنیاد علوم کاربردی رازی به شماره حساب 47000892552602 و شماره شبا IR920540109147000892552602");
                    column.Item().AlignRight().Text(
                        "و یا به حساب جام بانک ملت به شماره حساب 47901713/71 و شماره شبا IR430120000000004790171371 و یا شماره کارت 6104337926816255 واریز نمایید.");
                });

                page.Footer().BorderTop(1).BorderColor(Colors.Black).PaddingTop(2).Row(row =>
                {
                    row.RelativeItem().AlignCenter().Text("مهر و امضای خریدار").SemiBold();
                    row.RelativeItem().AlignCenter().Text("مهر و امضای فروشنده").SemiBold();
                    row.ConstantItem(90).AlignRight().Text("نسخه مشتری").SemiBold();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static string ResolveAgencyName(string agencyName)
    {
        var value = agencyName.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "بنیاد";
        }

        if (string.Equals(value, "آقای اسد لو", StringComparison.OrdinalIgnoreCase))
        {
            return "بنیاد";
        }

        return value;
    }

    private static IContainer OuterBorder(IContainer container)
    {
        return container.Border(1).BorderColor(Colors.Black);
    }

    private static IContainer SectionGridTitleCell(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Black)
            .PaddingVertical(2)
            .PaddingHorizontal(4)
            .AlignCenter()
            .AlignMiddle();
    }

    private static IContainer SectionGridCell(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Black)
            .PaddingVertical(2)
            .PaddingHorizontal(4)
            .AlignMiddle();
    }

    private static IContainer TableHeaderCell(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Black)
            .PaddingVertical(2)
            .PaddingHorizontal(2)
            .AlignCenter()
            .AlignMiddle();
    }

    private static IContainer TableBodyCell(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Black)
            .PaddingVertical(2)
            .PaddingHorizontal(2)
            .AlignMiddle();
    }

    private static string BuildPdfFileName(string billNo, Guid masterBillCode)
    {
        var safeValue = string.IsNullOrWhiteSpace(billNo) ? masterBillCode.ToString("N") : billNo.Trim();
        foreach (var invalidFileNameCharacter in Path.GetInvalidFileNameChars())
        {
            safeValue = safeValue.Replace(invalidFileNameCharacter, '_');
        }

        return $"Invoice_{safeValue}.pdf";
    }

    private static string ReadString(SqlDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return string.Empty;
        }

        return Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static int ReadInt32(SqlDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return 0;
        }

        return Convert.ToInt32(reader.GetValue(index), CultureInfo.InvariantCulture);
    }

    private static decimal ReadDecimal(SqlDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return 0;
        }

        return Convert.ToDecimal(reader.GetValue(index), CultureInfo.InvariantCulture);
    }

    private static string ResolveReceiptTypeInvoice(int? howToPay)
    {
        return howToPay switch
        {
            1 => "نقد",
            2 => "چک",
            3 => "حواله",
            4 => "فیش مالیاتی",
            9 => "کسر از کارمزد",
            10 => "بیمه",
            11 => "سوخت شده",
            12 => "تهاتر",
            13 => "تخفیف ایران مواد",
            14 => "تخفیف به مشتری",
            16 => "گرنت",
            _ => "نامشخص"
        };
    }

    private static string FormatAmount(decimal value)
    {
        var rounded = decimal.Round(value, 0, MidpointRounding.AwayFromZero);
        return ToPersianNumber(rounded.ToString("N0", CultureInfo.InvariantCulture));
    }

    private static string ToPersianNumber(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        return input
            .Replace('0', '۰')
            .Replace('1', '۱')
            .Replace('2', '۲')
            .Replace('3', '۳')
            .Replace('4', '۴')
            .Replace('5', '۵')
            .Replace('6', '۶')
            .Replace('7', '۷')
            .Replace('8', '۸')
            .Replace('9', '۹');
    }

    private byte[]? ResolveLogoBytes()
    {
        try
        {
            var candidates = new[]
            {
                Environment.GetEnvironmentVariable("INVOICE_LOGO_PATH"),
                Path.Combine(AppContext.BaseDirectory, "Assets", "logo-RASF1.png"),
                Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png"),
                Path.Combine(AppContext.BaseDirectory, "logo-RASF1.png"),
                Path.Combine(AppContext.BaseDirectory, "logo.png")
            };

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (File.Exists(candidate))
                {
                    return File.ReadAllBytes(candidate);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Invoice logo could not be loaded. PDF will be generated without logo.");
        }

        return null;
    }

    private readonly record struct InvoiceMetadata(
        Guid ContractCode,
        string BillNo,
        DateTime BillDate,
        decimal TotalPrice,
        string ContractNo);

    private readonly record struct CompanyWorkflowRawRow(
        int BillNo,
        DateTime BillDate,
        long Debtor,
        long Creditor,
        string ContractsNo,
        string AgencyName,
        string TypeInvoice);

    private readonly record struct InvoiceContractInfo(
        string CompanyName,
        string Address,
        string EconomicCode,
        string SetNum,
        string LetterDate,
        string LetterNo,
        string ContractNo,
        string PostCode,
        string BillDateDisplay,
        string FinancialCode,
        string NationalCode,
        string NationalArgument,
        string ExpertCompany,
        string AgencyName,
        string CustomerNumber,
        string CooperationName);

    private readonly record struct InvoiceLineItem(
        string MasterName,
        int NumberOfTests,
        decimal InvoicePrice,
        decimal TotalPrice,
        decimal Discount,
        decimal Tax,
        int BillNumber);

    private readonly record struct InvoicePdfPayload(
        InvoiceMetadata Metadata,
        InvoiceContractInfo Contract,
        IReadOnlyList<InvoiceLineItem> Items,
        int TaxRate);

    private readonly record struct SellerProfile(
        string Name,
        string Address,
        string EconomicCode,
        string RegistrationCode,
        string NationalId,
        string PostCode,
        string Phone)
    {
        public static SellerProfile Default =>
            new(
                "بنیاد علوم کاربردی رازی",
                "کیلومتر 12 جاده قدیم کرج، جنب پالایشگاه نفت پارس، ورودی سرخه حصار، خیابان فرنان (مرجان)، پلاک 72",
                "42030078101",
                "527",
                "10187003024",
                "7316413573",
                "02146841121 و 02149732");
    }
}

