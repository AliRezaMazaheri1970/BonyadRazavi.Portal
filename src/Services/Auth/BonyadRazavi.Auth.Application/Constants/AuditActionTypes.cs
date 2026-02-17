namespace BonyadRazavi.Auth.Application.Constants;

public static class AuditActionTypes
{
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Logout = "Logout";
    public const string TokenRefresh = "TokenRefresh";
    public const string TokenRevoke = "TokenRevoke";
    public const string ViewProfile = "ViewProfile";
    public const string ViewDashboard = "ViewDashboard";
    public const string ViewCompany = "ViewCompany";
    public const string UpdateCompany = "UpdateCompany";
    public const string ViewReport = "ViewReport";
    public const string ViewAuditLogs = "ViewAuditLogs";
    public const string ServiceHealth = "ServiceHealth";
    public const string SecurityDenied = "SecurityDenied";
    public const string ViewUsers = "ViewUsers";
    public const string ViewUser = "ViewUser";
    public const string CreateUser = "CreateUser";
    public const string UpdateUser = "UpdateUser";
}
