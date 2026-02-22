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
                    new("صورتحساب های صادر شده", "/invoices-issued"),
                    new("دریافت گزارشات", "/receive-reports"),
                    new("وضعیت مالی", "/finance"),
                    new("نظرسنجی", "/survey"),
                    new("شکایت", "/complaint"),
                    new("ارسال اطلاعات", "/upload"),
                    new("چت آنلاین", "/chat"),
                    new("دسته بندی مشتریان و امتیازدهی براساس کارکرد", "/customer-ranking"),
                    new("اطلاعات مازاد براساس آخرین گزارش", "/extra-info"),
                    new("ارسال لینک یکبار مصرف توسط پیامک", "/otp-link"),
                    new("تغییر رمز عبور", "/profile/change-password")
                };

                if (IsAdmin())
                {
                    items.Add(new SidebarItem("مدیریت کاربران", "/admin"));
                }

                return items;
            }
        }
        private bool isNavMenuOpen = false;

        private void ToggleNavMenu()
        {
            isNavMenuOpen = !isNavMenuOpen;
        }

        private void CloseMenu()
        {
            isNavMenuOpen = false;
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

        private async Task SignOut()
        {
            await UserSession.SignOutAsync();
            Navigation.NavigateTo("/login");
        }
    }
}
