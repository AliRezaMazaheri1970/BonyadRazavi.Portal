using System.Globalization;
using System.Text;
using BonyadRazavi.Shared.Contracts.Companies;
using BonyadRazavi.WebApp.Services;
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

        var csv = BuildExcelCsv(_rows);
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv);

        await using var stream = new MemoryStream(bytes);
        using var streamReference = new DotNetStreamReference(stream);

        var fileName = $"finance_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        await JsRuntime.InvokeVoidAsync("downloadFileFromStream", fileName, streamReference);
    }

    private static string BuildExcelCsv(IReadOnlyCollection<WorkflowRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",",
            EscapeCsv("شماره صورت حساب/رسید"),
            EscapeCsv("تاریخ صورت حساب/رسید"),
            EscapeCsv("رسید/صورت حساب"),
            EscapeCsv("قرارداد"),
            EscapeCsv("دفتر"),
            EscapeCsv("مبلغ بدهکاری"),
            EscapeCsv("مبلغ بستانکاری"),
            EscapeCsv("مانده خطی"),
            EscapeCsv("مانده تناوب")));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",",
                EscapeCsv(row.BillNo.ToString(CultureInfo.InvariantCulture)),
                EscapeCsv(FormatPersianDate(row.BillDate)),
                EscapeCsv(row.TypeInvoice),
                EscapeCsv(row.ContractsNo),
                EscapeCsv(row.AgencyName),
                EscapeCsv(FormatAmount(row.Debtor)),
                EscapeCsv(FormatAmount(row.Creditor)),
                EscapeCsv(FormatAmount(row.Remind)),
                EscapeCsv(FormatAmount(row.Reminding))));
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escapedValue = value.Replace("\"", "\"\"");
        return $"\"{escapedValue}\"";
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

