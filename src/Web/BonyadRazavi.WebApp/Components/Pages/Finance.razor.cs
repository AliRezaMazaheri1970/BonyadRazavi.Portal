using System.Globalization;
using BonyadRazavi.Shared.Contracts.Companies;
using BonyadRazavi.WebApp.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;

namespace BonyadRazavi.WebApp.Components.Pages;

public partial class Finance : ComponentBase
{
    private static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromSeconds(30);

    [Inject] public NavigationManager NavigationManager { get; set; } = default!;
    [Inject] public UserSession UserSession { get; set; } = default!;
    [Inject] public AuthApiClient AuthApiClient { get; set; } = default!;
    [Inject] public UsersApiClient UsersApiClient { get; set; } = default!;
    [Inject] public IJSRuntime JsRuntime { get; set; } = default!;

    private readonly List<WorkflowRow> _rows = [];
    private bool _isLoading;
    private string? _errorMessage;

    private long _totalDebtor;
    private long _totalCreditor;
    private long _lastLineBalance;
    private long _lastPeriodicBalance;

    protected override async Task OnInitializedAsync()
    {
        await UserSession.InitializeAsync();

        if (!UserSession.IsAuthenticated || UserSession.Current is null)
        {
            NavigationManager.NavigateTo("/login");
            return;
        }

        await LoadWorkflowAsync();
    }

    private async Task LoadWorkflowAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        _rows.Clear();
        _totalDebtor = 0;
        _totalCreditor = 0;
        _lastLineBalance = 0;
        _lastPeriodicBalance = 0;

        try
        {
            if (!await EnsureValidAccessTokenAsync())
            {
                return;
            }

            var token = UserSession.Current!.AccessToken;
            var result = await UsersApiClient.GetCompanyWorkflowAsync(token);
            if (!result.IsSuccess && result.StatusCode == StatusCodes.Status401Unauthorized)
            {
                if (!await TryRefreshSessionAsync())
                {
                    return;
                }

                result = await UsersApiClient.GetCompanyWorkflowAsync(UserSession.Current!.AccessToken);
            }

            if (!result.IsSuccess)
            {
                _errorMessage = result.ErrorMessage ?? "دریافت گردش حساب ناموفق بود.";
                return;
            }

            foreach (var row in result.Rows.Select(MapRow))
            {
                _rows.Add(row);
            }

            if (_rows.Count > 0)
            {
                _totalDebtor = _rows.Sum(row => row.Debtor);
                _totalCreditor = _rows.Sum(row => row.Creditor);
                _lastLineBalance = _rows.Last().Remind;
                _lastPeriodicBalance = _rows.Last().Reminding;
            }
        }
        catch
        {
            _errorMessage = "در دریافت گردش حساب خطا رخ داد.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task<bool> EnsureValidAccessTokenAsync()
    {
        if (UserSession.Current is null)
        {
            _errorMessage = "نشست کاربری معتبر نیست. لطفا دوباره وارد شوید.";
            NavigationManager.NavigateTo("/login");
            return false;
        }

        var hasValidAccessToken =
            !string.IsNullOrWhiteSpace(UserSession.Current.AccessToken) &&
            UserSession.Current.ExpiresAtUtc > DateTime.UtcNow.Add(TokenRefreshSkew);

        if (hasValidAccessToken)
        {
            return true;
        }

        return await TryRefreshSessionAsync();
    }

    private async Task<bool> TryRefreshSessionAsync()
    {
        var refreshToken = UserSession.Current?.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _errorMessage = "نشست کاربری معتبر نیست. لطفا دوباره وارد شوید.";
            NavigationManager.NavigateTo("/login");
            return false;
        }

        var refreshResult = await AuthApiClient.RefreshAsync(refreshToken);
        if (!refreshResult.IsSuccess || refreshResult.Payload is null)
        {
            _errorMessage = refreshResult.ErrorMessage ?? "نشست کاربری معتبر نیست. لطفا دوباره وارد شوید.";
            NavigationManager.NavigateTo("/login");
            return false;
        }

        await UserSession.SignInAsync(refreshResult.Payload);
        return true;
    }

    private static WorkflowRow MapRow(CompanyWorkflowDto row)
    {
        return new WorkflowRow
        {
            BillNo = row.BillNo,
            BillDate = row.BillDate,
            Debtor = row.Debtor,
            Creditor = row.Creditor,
            Remind = row.Remind,
            Reminding = row.Reminding,
            ContractsNo = string.IsNullOrWhiteSpace(row.ContractsNo) ? "-" : row.ContractsNo,
            AgencyName = string.IsNullOrWhiteSpace(row.AgencyName) ? "-" : row.AgencyName,
            TypeInvoice = string.IsNullOrWhiteSpace(row.TypeInvoice) ? "-" : row.TypeInvoice
        };
    }

    private static string FormatAmount(long value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string FormatPersianDate(DateTime value)
    {
        if (value == DateTime.MinValue)
        {
            return "-";
        }

        var calendar = new PersianCalendar();
        var year = calendar.GetYear(value);
        var month = calendar.GetMonth(value);
        var day = calendar.GetDayOfMonth(value);
        return $"{year:0000}/{month:00}/{day:00}";
    }

    private void GoToDashboard()
    {
        NavigationManager.NavigateTo("/dashboard");
    }

    private async Task ExportToExcelAsync()
    {
        if (_isLoading || _rows.Count == 0)
        {
            return;
        }

        var bytes = BuildExcelFile(_rows);

        await using var stream = new MemoryStream(bytes);
        using var streamReference = new DotNetStreamReference(stream);

        var fileName = $"finance_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        await JsRuntime.InvokeVoidAsync("downloadFileFromStream", fileName, streamReference);
    }

    private static byte[] BuildExcelFile(IReadOnlyCollection<WorkflowRow> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("گردش حساب");
        worksheet.RightToLeft = true;

        var headers = new[]
        {
            "شماره صورت حساب/رسید",
            "تاریخ صورت حساب/رسید",
            "رسید/صورت حساب",
            "قرارداد",
            "دفتر",
            "مبلغ بدهکاری",
            "مبلغ بستانکاری",
            "مانده خطی",
            "مانده تناوب"
        };

        for (var columnIndex = 0; columnIndex < headers.Length; columnIndex++)
        {
            worksheet.Cell(1, columnIndex + 1).Value = headers[columnIndex];
        }

        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E9ECFF");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        var rowIndex = 2;
        foreach (var row in rows)
        {
            worksheet.Cell(rowIndex, 1).Value = row.BillNo;
            worksheet.Cell(rowIndex, 2).Value = FormatPersianDate(row.BillDate);
            worksheet.Cell(rowIndex, 3).Value = row.TypeInvoice;
            worksheet.Cell(rowIndex, 4).Value = row.ContractsNo;
            worksheet.Cell(rowIndex, 5).Value = row.AgencyName;
            worksheet.Cell(rowIndex, 6).Value = row.Debtor;
            worksheet.Cell(rowIndex, 7).Value = row.Creditor;
            worksheet.Cell(rowIndex, 8).Value = row.Remind;
            worksheet.Cell(rowIndex, 9).Value = row.Reminding;
            rowIndex++;
        }

        if (rows.Count > 0)
        {
            worksheet.Cell(rowIndex, 1).Value = "جمع";
            worksheet.Range(rowIndex, 1, rowIndex, 5).Merge();
            worksheet.Cell(rowIndex, 6).FormulaA1 = $"SUM(F2:F{rowIndex - 1})";
            worksheet.Cell(rowIndex, 7).FormulaA1 = $"SUM(G2:G{rowIndex - 1})";
            worksheet.Cell(rowIndex, 8).Value = rows.Last().Remind;
            worksheet.Cell(rowIndex, 9).Value = rows.Last().Reminding;

            var totalRange = worksheet.Range(rowIndex, 1, rowIndex, 9);
            totalRange.Style.Font.Bold = true;
            totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F4FF");
            totalRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        worksheet.Columns(6, 9).Style.NumberFormat.Format = "#,##0";

        var usedRange = worksheet.RangeUsed();
        if (usedRange is not null)
        {
            usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            usedRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        worksheet.Columns(1, 9).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed class WorkflowRow
    {
        public int BillNo { get; init; }
        public DateTime BillDate { get; init; }
        public long Debtor { get; init; }
        public long Creditor { get; init; }
        public long Remind { get; init; }
        public long Reminding { get; init; }
        public string ContractsNo { get; init; } = string.Empty;
        public string AgencyName { get; init; } = string.Empty;
        public string TypeInvoice { get; init; } = string.Empty;
    }
}
