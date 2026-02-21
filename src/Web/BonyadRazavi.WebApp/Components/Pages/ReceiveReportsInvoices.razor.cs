using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop; // ضروری برای تقویم
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BonyadRazavi.WebApp.Components.Pages
{
    public partial class ReceiveReportsInvoices : ComponentBase, IDisposable
    {
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;
        [Inject] public IJSRuntime JS { get; set; } = default!; // تزریق جاوااسکریپت

        public class Invoice
        {
            public string InvoiceName { get; set; } = string.Empty;
            public DateTime InvoiceDate { get; set; }
            public string ContractNumber { get; set; } = string.Empty;

            public int TotalPrice { get; set; }
        }

        private List<Invoice> invoices = new();
        private string searchName = "";
        private string searchContract = "";
        private int? searchTotalPrice;

        // برگرداندن به استرینگ برای جلوگیری از خطای سال ۱۴۰۴ در دات‌نت
        private string searchFromDate = "";
        private string searchToDate = "";

        private string sortColumn = "";
        private bool isAscending = true;

        private DotNetObjectReference<ReceiveReportsInvoices>? dotNetRef;

        protected override void OnInitialized()
        {
            invoices = new List<Invoice>
            {
                new Invoice { InvoiceName = "هزینه سرور", InvoiceDate = new DateTime(1404, 10, 05), ContractNumber = "CTR-1001" , TotalPrice = 178000000 },
                new Invoice { InvoiceName = "پشتیبانی نرم‌افزار", InvoiceDate = new DateTime(1404, 10, 12), ContractNumber = "CTR-1005" , TotalPrice = 245000000 },
                new Invoice { InvoiceName = "خرید تجهیزات", InvoiceDate = new DateTime(1404, 09, 20), ContractNumber = "CTR-1002" , TotalPrice = 79000000 },
                new Invoice { InvoiceName = "اینترنت", InvoiceDate = new DateTime(1404, 10, 01), ContractNumber = "CTR-1003" , TotalPrice = 119000000},
              
              

            };
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                dotNetRef = DotNetObjectReference.Create(this);
                // فعال‌سازی تقویم‌های جاوااسکریپت پس از لود صفحه
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

        private IEnumerable<Invoice> FilteredAndSortedInvoices
        {
            get
            {
                var query = invoices.AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchName))
                    query = query.Where(i => i.InvoiceName.Contains(searchName, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(searchContract))
                    query = query.Where(i => i.ContractNumber.Contains(searchContract, StringComparison.OrdinalIgnoreCase));

                // فیلتر متنی از تاریخ
                if (!string.IsNullOrWhiteSpace(searchFromDate))
                    query = query.Where(i => string.Compare(i.InvoiceDate.ToString("yyyy/MM/dd"), searchFromDate) >= 0);

                // فیلتر متنی تا تاریخ
                if (!string.IsNullOrWhiteSpace(searchToDate))
                    query = query.Where(i => string.Compare(i.InvoiceDate.ToString("yyyy/MM/dd"), searchToDate) <= 0);

                if (searchTotalPrice.HasValue && searchTotalPrice.Value > 0)
                {
                    // تبدیل عدد به متن برای جستجوی "شامل بودن"
                    query = query.Where(i => i.TotalPrice.ToString().Contains(searchTotalPrice.Value.ToString()));
                }

                if (!string.IsNullOrEmpty(sortColumn))
                {
                    if (sortColumn == "Name") query = isAscending ? query.OrderBy(i => i.InvoiceName) : query.OrderByDescending(i => i.InvoiceName);
                    else if (sortColumn == "Date") query = isAscending ? query.OrderBy(i => i.InvoiceDate) : query.OrderByDescending(i => i.InvoiceDate);
                    else if (sortColumn == "Contract") query = isAscending ? query.OrderBy(i => i.ContractNumber) : query.OrderByDescending(i => i.ContractNumber);
                    else if (sortColumn == "Price") query = isAscending ? query.OrderBy(i => i.TotalPrice) : query.OrderByDescending(i => i.TotalPrice);
                }

                return query.ToList();
            }
        }

        private void Sort(string column) { if (sortColumn == column) isAscending = !isAscending; else { sortColumn = column; isAscending = true; } }
        private string GetSortIcon(string column) { if (sortColumn != column) return "↕"; return isAscending ? "↑" : "↓"; }
        private void GoToDashboard() { NavigationManager.NavigateTo("/dashboard"); }
        //private void DownloadInvoice(Invoice selectedInvoice) { Console.WriteLine($"Downloading invoice for: {selectedInvoice.ContractNumber}"); }

        public void Dispose() { dotNetRef?.Dispose(); }


        private async Task ClearFromDate()
        {
            searchFromDate = ""; 

            await JS.InvokeVoidAsync("clearInputValue", "fromDate");

            StateHasChanged(); 
        }

        private async Task ClearToDate()
        {
            searchToDate = ""; 

            await JS.InvokeVoidAsync("clearInputValue", "toDate");

            StateHasChanged(); 
        }

        // متد دانلود صورتحساب تغییر یافت
        private async Task DownloadInvoice(Invoice selectedInvoice)
        {
            // ۱. تولید محتوای فایل (در پروژه واقعی، اینجا فایل PDF یا اکسل تولید می‌شود)
            string fileContent = $"بِسْمِ اللَّهِ الرَّحْمَنِ الرَّحِيمِ\n\n" +
                                 $"--- جزئیات صورتحساب ---\n" +
                                 $"عنوان: {selectedInvoice.InvoiceName}\n" +
                                 $"شماره قرارداد: {selectedInvoice.ContractNumber}\n" +
                                 $"تاریخ ثبت: {selectedInvoice.InvoiceDate.ToString("yyyy/MM/dd")}\n" +
                                 $"-----------------------\n" +
                                 $"سیستم یکپارچه گزارشات";

            // تبدیل متن به آرایه‌ای از بایت‌ها
            var fileBytes = System.Text.Encoding.UTF8.GetBytes(fileContent);

            // ۲. تعیین نام فایلی که کاربر دانلود خواهد کرد
            string fileName = $"Invoice_{selectedInvoice.ContractNumber}.txt";

            // ۳. ایجاد یک استریم (جریان داده) و ارسال آن به مرورگر کاربر از طریق جاوااسکریپت
            using var stream = new MemoryStream(fileBytes);
            using var streamRef = new DotNetStreamReference(stream: stream);

            // فراخوانی تابع جاوااسکریپتی که در فایل App.razor نوشتیم
            await JS.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
        }

        private void OnPriceChanged(ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out var result))
                searchTotalPrice = result;
            else
                searchTotalPrice = null;

            StateHasChanged(); // اجبار به بازخوانی FilteredAndSortedInvoices
        }
    }
}
