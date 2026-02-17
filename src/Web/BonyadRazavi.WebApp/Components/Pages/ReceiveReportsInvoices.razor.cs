using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BonyadRazavi.WebApp.Components.Pages
{
    // کلمه partial بسیار مهم است، زیرا این کلاس با فایل razor ترکیب می‌شود
    public partial class ReceiveReportsInvoices : ComponentBase
    {
        // جایگزین @inject در فایل‌های C#
        [Inject]
        public NavigationManager NavigationManager { get; set; } = default!;

        // 1. مدل داده‌ها
        public class Invoice
        {
            public string InvoiceName { get; set; } = string.Empty;
            public DateTime InvoiceDate { get; set; }
            public string ContractNumber { get; set; } = string.Empty;
        }

        // 2. متغیرها
        private List<Invoice> invoices = new();
        private string searchName = "";
        private string searchDate = "";
        private string searchContract = "";
        private string sortColumn = "";
        private bool isAscending = true;

        protected override void OnInitialized()
        {
            invoices = new List<Invoice>
            {
                new Invoice { InvoiceName = "هزینه سرور", InvoiceDate = new DateTime(2023, 10, 05), ContractNumber = "CTR-1001" },
                new Invoice { InvoiceName = "پشتیبانی نرم‌افزار", InvoiceDate = new DateTime(2023, 10, 12), ContractNumber = "CTR-1005" },
                new Invoice { InvoiceName = "خرید تجهیزات", InvoiceDate = new DateTime(2023, 09, 20), ContractNumber = "CTR-1002" },
                new Invoice { InvoiceName = "اینترنت", InvoiceDate = new DateTime(2023, 10, 01), ContractNumber = "CTR-1003" },
                new Invoice { InvoiceName = "هزینه سرور", InvoiceDate = new DateTime(2023, 10, 05), ContractNumber = "CTR-1001" },
                new Invoice { InvoiceName = "پشتیبانی نرم‌افزار", InvoiceDate = new DateTime(2023, 10, 12), ContractNumber = "CTR-1005" },
                new Invoice { InvoiceName = "خرید تجهیزات", InvoiceDate = new DateTime(2023, 09, 20), ContractNumber = "CTR-1002" },
                new Invoice { InvoiceName = "اینترنت", InvoiceDate = new DateTime(2023, 10, 01), ContractNumber = "CTR-1003" },
                new Invoice { InvoiceName = "هزینه سرور", InvoiceDate = new DateTime(2023, 10, 05), ContractNumber = "CTR-1001" },
                new Invoice { InvoiceName = "پشتیبانی نرم‌افزار", InvoiceDate = new DateTime(2023, 10, 12), ContractNumber = "CTR-1005" },
                new Invoice { InvoiceName = "خرید تجهیزات", InvoiceDate = new DateTime(2023, 09, 20), ContractNumber = "CTR-1002" },
                new Invoice { InvoiceName = "اینترنت", InvoiceDate = new DateTime(2023, 10, 01), ContractNumber = "CTR-1003" },
                new Invoice { InvoiceName = "هزینه سرور", InvoiceDate = new DateTime(2023, 10, 05), ContractNumber = "CTR-1001" },
                new Invoice { InvoiceName = "پشتیبانی نرم‌افزار", InvoiceDate = new DateTime(2023, 10, 12), ContractNumber = "CTR-1005" },
                new Invoice { InvoiceName = "خرید تجهیزات", InvoiceDate = new DateTime(2023, 09, 20), ContractNumber = "CTR-1002" },
                new Invoice { InvoiceName = "اینترنت", InvoiceDate = new DateTime(2023, 10, 01), ContractNumber = "CTR-1003" },
                new Invoice { InvoiceName = "هزینه سرور", InvoiceDate = new DateTime(2023, 10, 05), ContractNumber = "CTR-1001" },
                new Invoice { InvoiceName = "پشتیبانی نرم‌افزار", InvoiceDate = new DateTime(2023, 10, 12), ContractNumber = "CTR-1005" },
                new Invoice { InvoiceName = "خرید تجهیزات", InvoiceDate = new DateTime(2023, 09, 20), ContractNumber = "CTR-1002" },
                new Invoice { InvoiceName = "اینترنت", InvoiceDate = new DateTime(2023, 10, 01), ContractNumber = "CTR-1003" },
                new Invoice { InvoiceName = "هزینه سرور", InvoiceDate = new DateTime(2023, 10, 05), ContractNumber = "CTR-1001" },
                new Invoice { InvoiceName = "پشتیبانی نرم‌افزار", InvoiceDate = new DateTime(2023, 10, 12), ContractNumber = "CTR-1005" },
                new Invoice { InvoiceName = "خرید تجهیزات", InvoiceDate = new DateTime(2023, 09, 20), ContractNumber = "CTR-1002" },
                new Invoice { InvoiceName = "اینترنت", InvoiceDate = new DateTime(2023, 10, 01), ContractNumber = "CTR-1003" },
                new Invoice { InvoiceName = "هزینه سرور", InvoiceDate = new DateTime(2023, 10, 05), ContractNumber = "CTR-1001" },
                new Invoice { InvoiceName = "پشتیبانی نرم‌افزار", InvoiceDate = new DateTime(2023, 10, 12), ContractNumber = "CTR-1005" },
                new Invoice { InvoiceName = "خرید تجهیزات", InvoiceDate = new DateTime(2023, 09, 20), ContractNumber = "CTR-1002" },
                new Invoice { InvoiceName = "اینترنت", InvoiceDate = new DateTime(2023, 10, 01), ContractNumber = "CTR-1003" },
                new Invoice { InvoiceName = "هزینه سرور", InvoiceDate = new DateTime(2023, 10, 05), ContractNumber = "CTR-1001" },
                new Invoice { InvoiceName = "پشتیبانی نرم‌افزار", InvoiceDate = new DateTime(2023, 10, 12), ContractNumber = "CTR-1005" },
                new Invoice { InvoiceName = "خرید تجهیزات", InvoiceDate = new DateTime(2023, 09, 20), ContractNumber = "CTR-1002" },
                new Invoice { InvoiceName = "اینترنت", InvoiceDate = new DateTime(2023, 10, 01), ContractNumber = "CTR-1003" },
                new Invoice { InvoiceName = "هزینه سرور", InvoiceDate = new DateTime(2023, 10, 05), ContractNumber = "CTR-1001" },
                new Invoice { InvoiceName = "پشتیبانی نرم‌افزار", InvoiceDate = new DateTime(2023, 10, 12), ContractNumber = "CTR-1005" },
                new Invoice { InvoiceName = "خرید تجهیزات", InvoiceDate = new DateTime(2023, 09, 20), ContractNumber = "CTR-1002" },
                new Invoice { InvoiceName = "اینترنت", InvoiceDate = new DateTime(2023, 10, 01), ContractNumber = "CTR-1003" }
            };
        }

        // 3. پراپرتی محاسبه‌گر برای فیلتر و مرتب‌سازی
        private IEnumerable<Invoice> FilteredAndSortedInvoices
        {
            get
            {
                var query = invoices.AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchName))
                    query = query.Where(i => i.InvoiceName.Contains(searchName, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(searchDate))
                    query = query.Where(i => i.InvoiceDate.ToString("yyyy/MM/dd").Contains(searchDate));

                if (!string.IsNullOrWhiteSpace(searchContract))
                    query = query.Where(i => i.ContractNumber.Contains(searchContract, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(sortColumn))
                {
                    if (sortColumn == "Name")
                        query = isAscending ? query.OrderBy(i => i.InvoiceName) : query.OrderByDescending(i => i.InvoiceName);
                    else if (sortColumn == "Date")
                        query = isAscending ? query.OrderBy(i => i.InvoiceDate) : query.OrderByDescending(i => i.InvoiceDate);
                    else if (sortColumn == "Contract")
                        query = isAscending ? query.OrderBy(i => i.ContractNumber) : query.OrderByDescending(i => i.ContractNumber);
                }

                return query.ToList();
            }
        }

        // 4. متد مدیریت تغییر وضعیت مرتب‌سازی
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

        // 5. متد نمایش آیکون
        private string GetSortIcon(string column)
        {
            if (sortColumn != column) return "↕";
            return isAscending ? "↑" : "↓";
        }

        // 6. متد انتقال به داشبورد
        private void GoToDashboard()
        {
            NavigationManager.NavigateTo("/dashboard");
        }
    }
}
