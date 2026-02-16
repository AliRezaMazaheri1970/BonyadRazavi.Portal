using BonyadRazavi.WebApp.Services;

namespace BonyadRazavi.WebApp.Components.Pages
{
    public partial class Dashboard
    {

        private sealed record DashboardTile(string Title, string IconPath, string Route);

        private static readonly DashboardTile[] DashboardTiles =
        [
        new("دریافت گزارش و صورتحساب", "Images/dashboard/reports.png", "/reports"),
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


        private sealed record SidebarItem(string Title, string Route);

        private static readonly SidebarItem[] SidebarItems =
        [
            new("پذیرش آنلاین + هوش مصنوعی", "/ai-admission"),
    new("اعلام هزینه", "/cost"),
    new("پیگیری مرحله آزمایش", "/lab-tracking"),
    new("دریافت گزارش و صورتحساب", "/reports"),
    new("وضعیت مالی", "/finance"),
    new("نظرسنجی", "/survey"),
    new("شکایت", "/complaint"),
    new("ارسال اطلاعات", "/upload"),
    new("چت آنلاین", "/chat"),
    new("دسته بندی مشتریان و امتیازدهی براساس کارکرد", "/customer-ranking"),
    new("اطلاعات مازاد براساس آخرین گزارش", "/extra-info"),
    new("ارسال لینک یکبار مصرف توسط پیامک", "/otp-link")
        ];


        protected override void OnInitialized()
        {
            if (!UserSession.IsAuthenticated)
            {
                Navigation.NavigateTo("/login");
            }
        }

        private void SignOut()
        {
            UserSession.SignOut();
            Navigation.NavigateTo("/login");
        }

        private void GoToLogin()
        {
            Navigation.NavigateTo("/login");
        }

        private static string GetInitials(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "BR";
            }

            var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                return parts[0][..1];
            }

            return string.Concat(parts[0][..1], parts[^1][..1]);
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
