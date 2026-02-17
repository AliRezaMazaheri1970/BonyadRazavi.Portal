using System.Security.Claims;

namespace BonyadRazavi.Auth.Api.Security;

public static class PortalRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string CompanyAdmin = "CompanyAdmin";
    public const string Manager = "Manager";
    public const string User = "User";
    public const string Auditor = "Auditor";
    public const string Operator = "Operator";

    public static bool HasRole(ClaimsPrincipal principal, string role)
    {
        return principal.IsInRole(role);
    }
}
