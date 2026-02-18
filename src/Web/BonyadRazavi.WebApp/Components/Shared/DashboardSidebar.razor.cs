using BonyadRazavi.WebApp.Services;

namespace BonyadRazavi.WebApp.Components.Shared
{
    public partial class DashboardSidebar
    {
        private sealed record SidebarItem(string Title, string Route);

        private IEnumerable<SidebarItem> SidebarItems
        {
            get
            {
                var items = new List<SidebarItem>
                {
                    new("پذیرش آنلاین + هوش مصنوعی", "/ai-admission"),
                    new("اعلام هزینه", "/cost"),
                    new("پیگیری مرحله آزمایش", "/lab-tracking"),
                    new("دریافت گزارش و صورتحساب", "/Receive-reports-invoices"),
                    new("وضعیت مالی", "/finance"),
                    new("نظرسنجی", "/survey"),
                    new("شکایت", "/complaint"),
                    new("ارسال اطلاعات", "/upload"),
                    new("چت آنلاین", "/chat"),
                    new("دسته بندی مشتریان و امتیازدهی براساس کارکرد", "/customer-ranking"),
                    new("اطلاعات مازاد براساس آخرین گزارش", "/extra-info"),
                    new("ارسال لینک یکبار مصرف توسط پیامک", "/otp-link")
                };

                if (IsAdmin())
                {
                    items.Add(new SidebarItem("مدیریت کاربران", "/admin"));
                }

                return items;
            }
        }

        private bool IsAdmin()
        {
            var roles = UserSession.Current?.Roles;
            if (roles is null || roles.Count == 0)
            {
                return false;
            }

            return roles.Any(role =>
                string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase));
        }

        private void SignOut()
        {
            UserSession.SignOut();
            Navigation.NavigateTo("/login");
        }
    }
}
