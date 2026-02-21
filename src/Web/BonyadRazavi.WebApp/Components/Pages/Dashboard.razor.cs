using BonyadRazavi.WebApp.Services;

namespace BonyadRazavi.WebApp.Components.Pages
{
    public partial class Dashboard
    {
        private sealed record DashboardTile(string Title, string IconPath, string Route);

        private static readonly DashboardTile[] DashboardTiles =
        [
            new("دریافت گزارش و صورتحساب", "Images/dashboard/reports.png", "/Receive-reports-invoices"),
            new("پیگیری مرحله آزمایش", "Images/dashboard/lab.png", "/lab-tracking"),
            new("اعلام هزینه", "Images/dashboard/cost.png", "/cost"),
            new("شکایت", "Images/dashboard/complaint.png", "/complaint"),
            new("نظرسنجی", "Images/dashboard/survey.png", "/survey"),
            new("وضعیت مالی", "Images/dashboard/finance.png", "/finance"),
            new("ارسال اطلاعات", "Images/dashboard/SendInformation.png", "/cost"),
            new("ارسال لینک یکبار مصرف توسط پیامک", "Images/dashboard/sms.png", "/complaint"),
            new("چت آنلاین", "Images/dashboard/chat-online.png", "/survey"),
            new("دسته بندی مشتریان و امتیازدهی", "Images/dashboard/category-customer.png", "/reports"),
            new("اطلاعات مازاد براساس آخرین گزارش", "Images/dashboard/information.png", "/finance"),
            new("پذیرش آنلاین", "Images/dashboard/reception-online.png", "/lab-tracking")
        ];

        protected override async Task OnInitializedAsync()
        {
            await UserSession.InitializeAsync();

            if (!UserSession.IsAuthenticated)
            {
                Navigation.NavigateTo("/login");
            }
        }

        private void GoToLogin()
        {
            Navigation.NavigateTo("/login");
        }

        private static string GetTodayPersianText()
        {
            var now = DateTime.Now;
            var persian = new System.Globalization.PersianCalendar();

            var dayName = now.DayOfWeek switch
            {
                DayOfWeek.Saturday => "شنبه",
                DayOfWeek.Sunday => "یکشنبه",
                DayOfWeek.Monday => "دوشنبه",
                DayOfWeek.Tuesday => "سه شنبه",
                DayOfWeek.Wednesday => "چهارشنبه",
                DayOfWeek.Thursday => "پنجشنبه",
                _ => "جمعه"
            };

            var day = persian.GetDayOfMonth(now);
            var month = persian.GetMonth(now);
            var year = persian.GetYear(now);

            return $"{dayName} : {day:00} / {month:00} / {year}";
        }
    }
}
