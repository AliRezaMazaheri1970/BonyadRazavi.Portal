using BonyadRazavi.Shared.Contracts.Companies;
using BonyadRazavi.WebApp.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;
using System.Globalization;

namespace BonyadRazavi.WebApp.Components.Pages;

public partial class InvoicesIssued : ComponentBase, IDisposable
{
    private static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromSeconds(30);

    [Inject] public NavigationManager NavigationManager { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;
    [Inject] public UserSession UserSession { get; set; } = default!;
    [Inject] public AuthApiClient AuthApiClient { get; set; } = default!;
    [Inject] public UsersApiClient UsersApiClient { get; set; } = default!;

    public class Invoice
    {
        public Guid MasterBillCode { get; set; }
        public string InvoiceName { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public long? InvoiceNumber { get; set; }
        public long? ContractNumberNumeric { get; set; }
        public decimal TotalPrice { get; set; }
    }

    private List<Invoice> invoices = [];
    private string searchName = string.Empty;
    private string searchContract = string.Empty;
    private decimal? searchTotalPrice;
    private string searchFromDate = string.Empty;
    private string searchToDate = string.Empty;
    private string sortColumn = string.Empty;
    private bool isAscending = true;
    private bool _isLoading;
    private string? _errorMessage;
    private DotNetObjectReference<InvoicesIssued>? dotNetRef;

    protected override async Task OnInitializedAsync()
    {
        await UserSession.InitializeAsync();

        if (!UserSession.IsAuthenticated || UserSession.Current is null)
        {
            NavigationManager.NavigateTo("/login");
            return;
        }

        await LoadInvoicesAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            dotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("initShamsiPicker", "fromDate", dotNetRef, nameof(SetFromDate));
            await JS.InvokeVoidAsync("initShamsiPicker", "toDate", dotNetRef, nameof(SetToDate));
        }
    }

    [JSInvokable]
    public void SetFromDate(string date)
    {
        searchFromDate = date;
        StateHasChanged();
    }

    [JSInvokable]
    public void SetToDate(string date)
    {
        searchToDate = date;
        StateHasChanged();
    }

    //private IEnumerable<Invoice> FilteredAndSortedInvoices
    //{
    //    get
    //    {
    //        var query = invoices.AsQueryable();

    //        if (!string.IsNullOrWhiteSpace(searchName))
    //        {
    //            query = query.Where(i => i.InvoiceName.Contains(searchName, StringComparison.OrdinalIgnoreCase));
    //        }

    //        if (!string.IsNullOrWhiteSpace(searchContract))
    //        {
    //            query = query.Where(i => i.ContractNumber.Contains(searchContract, StringComparison.OrdinalIgnoreCase));
    //        }

    //        if (!string.IsNullOrWhiteSpace(searchFromDate))
    //        {
    //            query = query.Where(i =>
    //                string.Compare(i.InvoiceDate.ToString("yyyy/MM/dd"), searchFromDate, StringComparison.Ordinal) >= 0);
    //        }

    //        if (!string.IsNullOrWhiteSpace(searchToDate))
    //        {
    //            query = query.Where(i =>
    //                string.Compare(i.InvoiceDate.ToString("yyyy/MM/dd"), searchToDate, StringComparison.Ordinal) <= 0);
    //        }

    //        if (searchTotalPrice.HasValue && searchTotalPrice.Value > 0)
    //        {
    //            query = query.Where(i => i.TotalPrice >= searchTotalPrice.Value);
    //        }

    //        if (!string.IsNullOrEmpty(sortColumn))
    //        {
    //            query = sortColumn switch
    //            {
    //                "Name" => isAscending ? query.OrderBy(i => i.InvoiceName) : query.OrderByDescending(i => i.InvoiceName),
    //                "Date" => isAscending ? query.OrderBy(i => i.InvoiceDate) : query.OrderByDescending(i => i.InvoiceDate),
    //                "Contract" => isAscending
    //                    ? query.OrderBy(i => i.ContractNumber)
    //                    : query.OrderByDescending(i => i.ContractNumber),
    //                "Price" => isAscending ? query.OrderBy(i => i.TotalPrice) : query.OrderByDescending(i => i.TotalPrice),
    //                _ => query
    //            };
    //        }

    //        return query.ToList();
    //    }
    //}


    private IEnumerable<Invoice> FilteredAndSortedInvoices
    {
        get
        {
            var query = invoices.AsQueryable();

            // فیلترهای جستجو (همچنان روی فیلدهای رشته‌ای)
            if (!string.IsNullOrWhiteSpace(searchName))
                query = query.Where(i => i.InvoiceName.Contains(searchName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(searchContract))
                query = query.Where(i => i.ContractNumber.Contains(searchContract, StringComparison.OrdinalIgnoreCase));

            // فیلتر تاریخ (با تبدیل به شمسی)
            if (!string.IsNullOrWhiteSpace(searchFromDate))
                query = query.Where(i => string.Compare(ToPersianDate(i.InvoiceDate), searchFromDate, StringComparison.Ordinal) >= 0);
            if (!string.IsNullOrWhiteSpace(searchToDate))
                query = query.Where(i => string.Compare(ToPersianDate(i.InvoiceDate), searchToDate, StringComparison.Ordinal) <= 0);

            // فیلتر قیمت
            if (searchTotalPrice.HasValue)
                query = query.Where(i => i.TotalPrice >= searchTotalPrice.Value);

            // مرتب‌سازی (با استفاده از فیلدهای عددی)
            if (!string.IsNullOrEmpty(sortColumn))
            {
                query = sortColumn switch
                {
                    "Name" => isAscending
                        ? query.OrderBy(i => i.InvoiceNumber ?? long.MaxValue)
                        : query.OrderByDescending(i => i.InvoiceNumber ?? long.MaxValue),
                    "Contract" => isAscending
                        ? query.OrderBy(i => i.ContractNumberNumeric ?? long.MaxValue)
                        : query.OrderByDescending(i => i.ContractNumberNumeric ?? long.MaxValue),
                    "Date" => isAscending
                        ? query.OrderBy(i => i.InvoiceDate)
                        : query.OrderByDescending(i => i.InvoiceDate),
                    "Price" => isAscending
                        ? query.OrderBy(i => i.TotalPrice)
                        : query.OrderByDescending(i => i.TotalPrice),
                    _ => query
                };
            }

            return query.ToList();
        }
    }

    private async Task LoadInvoicesAsync()
    {
        _isLoading = true;
        _errorMessage = null;

        try
        {
            if (!await EnsureValidAccessTokenAsync())
            {
                invoices = [];
                return;
            }

            var token = UserSession.Current!.AccessToken;
            var result = await UsersApiClient.GetCompanyInvoicesAsync(token);
            if (!result.IsSuccess && result.StatusCode == StatusCodes.Status401Unauthorized)
            {
                if (!await TryRefreshSessionAsync())
                {
                    invoices = [];
                    return;
                }

                result = await UsersApiClient.GetCompanyInvoicesAsync(UserSession.Current!.AccessToken);
            }

            if (!result.IsSuccess)
            {
                _errorMessage = result.ErrorMessage ?? "دریافت لیست صورتحساب‌ها ناموفق بود.";
                invoices = [];
                return;
            }

            invoices = result.Invoices
                .Select(MapInvoice)
                .ToList();
        }
        catch
        {
            _errorMessage = "در دریافت لیست صورتحساب‌ها خطا رخ داد.";
            invoices = [];
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

    private static Invoice MapInvoice(CompanyInvoiceDto invoice)
    {
        long.TryParse(invoice.BillNo, out long invoiceNumber);
        long.TryParse(invoice.ContractNo, out long contractNumber);

        return new Invoice
        {
            MasterBillCode = invoice.MasterBillCode,
            InvoiceName = invoice.BillNo,
            InvoiceNumber = invoiceNumber == 0 ? null : invoiceNumber, // صفر به‌عنوان مقدار معتبر در نظر گرفته نشود (اختیاری)
            InvoiceDate = invoice.BillDate,
            ContractNumber = string.IsNullOrWhiteSpace(invoice.ContractNo) ? "-" : invoice.ContractNo,
            ContractNumberNumeric = contractNumber == 0 ? null : contractNumber,
            TotalPrice = invoice.TotalPrice
        };
    }

    private void Sort(string column)
    {
        if (sortColumn == column)
        {
            isAscending = !isAscending;
        }
        else
        {
            sortColumn = column;
            isAscending = true;
        }
    }

    private string GetSortIcon(string column)
    {
        if (sortColumn != column)
        {
            return "↕"; 
        }
        return isAscending ? "↓" :  "↑";
    }

    private void GoToDashboard()
    {
        NavigationManager.NavigateTo("/dashboard");
    }

    public void Dispose()
    {
        dotNetRef?.Dispose();
    }

    private async Task ClearFromDate()
    {
        searchFromDate = string.Empty;
        await JS.InvokeVoidAsync("clearInputValue", "fromDate");
        StateHasChanged();
    }

    private async Task ClearToDate()
    {
        searchToDate = string.Empty;
        await JS.InvokeVoidAsync("clearInputValue", "toDate");
        StateHasChanged();
    }

    private async Task DownloadInvoice(Invoice selectedInvoice)
    {
        if (selectedInvoice.MasterBillCode == Guid.Empty)
        {
            _errorMessage = "کد صورتحساب معتبر نیست.";
            await JS.InvokeVoidAsync("alert", _errorMessage);
            return;
        }

        if (!await EnsureValidAccessTokenAsync())
        {
            return;
        }

        var token = UserSession.Current!.AccessToken;
        var result = await UsersApiClient.DownloadCompanyInvoicePdfAsync(token, selectedInvoice.MasterBillCode);
        if (!result.IsSuccess && result.StatusCode == StatusCodes.Status401Unauthorized)
        {
            if (!await TryRefreshSessionAsync())
            {
                return;
            }

            result = await UsersApiClient.DownloadCompanyInvoicePdfAsync(
                UserSession.Current!.AccessToken,
                selectedInvoice.MasterBillCode);
        }

        if (!result.IsSuccess || result.FileBytes.Length == 0)
        {
            _errorMessage = result.ErrorMessage ?? "دانلود صورتحساب ناموفق بود.";
            await JS.InvokeVoidAsync("alert", _errorMessage);
            return;
        }

        var fileName = string.IsNullOrWhiteSpace(result.FileName)
            ? $"Invoice_{selectedInvoice.InvoiceName}.pdf"
            : result.FileName;

        using var stream = new MemoryStream(result.FileBytes);
        using var streamRef = new DotNetStreamReference(stream: stream);
        await JS.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);

    }


    private string ToPersianDate(DateTime gregorianDate)
    {
        PersianCalendar pc = new PersianCalendar();
        return $"{pc.GetYear(gregorianDate):0000}/{pc.GetMonth(gregorianDate):00}/{pc.GetDayOfMonth(gregorianDate):00}";
    }

}
