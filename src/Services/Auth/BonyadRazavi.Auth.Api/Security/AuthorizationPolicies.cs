namespace BonyadRazavi.Auth.Api.Security;

public static class AuthorizationPolicies
{
    public const string AuthenticatedUser = "Authenticated.User";
    public const string PortalAccess = "Portal.Access";
    public const string UsersRead = "Users.Read";
    public const string UsersManage = "Users.Manage";
    public const string CompaniesRead = "Companies.Read";
    public const string CompaniesManage = "Companies.Manage";
    public const string AuditRead = "Audit.Read";
    public const string SystemAdmin = "System.Admin";
}
