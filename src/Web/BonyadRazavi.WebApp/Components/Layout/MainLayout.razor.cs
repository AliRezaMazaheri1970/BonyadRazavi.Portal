//using System;
//using Microsoft.AspNetCore.Components;
//using Microsoft.AspNetCore.Components.Routing;
//using BonyadRazavi.WebApp.Services;

//namespace BonyadRazavi.WebApp.Components.Layout
//{
//    public partial class MainLayout : IDisposable
//    {
//        private bool _shouldShowSidebar;

//        protected override void OnInitialized()
//        {
//            UpdateSidebarVisibility();
//            Navigation.LocationChanged += OnLocationChanged;
//        }

//        private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
//        {
//            UpdateSidebarVisibility();
//            StateHasChanged();
//        }

//        private void UpdateSidebarVisibility()
//        {
//            try
//            {
//                // چک کردن اینکه آیا کاربر لاگین کرده است
//                if (!UserSession.IsAuthenticated || UserSession.Current is null)
//                {
//                    _shouldShowSidebar = false;
//                    return;
//                }

//                // چک کردن اینکه آیا در صفحه لاگین هستیم یا نه
//                var uri = Navigation.Uri;
//                var relativePath = Navigation.ToBaseRelativePath(uri).TrimStart('/');
//                var isLoginPage = string.IsNullOrEmpty(relativePath) || 
//                                relativePath.Equals("login", StringComparison.OrdinalIgnoreCase);
                
//                _shouldShowSidebar = !isLoginPage;
//            }
//            catch
//            {
//                // در صورت بروز خطا، سایدبار را مخفی می‌کنیم
//                _shouldShowSidebar = false;
//            }
//        }

//        public void Dispose()
//        {
//            Navigation.LocationChanged -= OnLocationChanged;
//        }
//    }
//}
