using System.Security.Claims;

namespace BonyadRazavi.Auth.Api.Security;

public static class TenantContextResolver
{
    public static bool TryGetCompanyCode(ClaimsPrincipal principal, out Guid companyCode)
    {
        var rawCompanyCode = principal.FindFirstValue("company_code");
        return Guid.TryParse(rawCompanyCode, out companyCode);
    }

    public static bool CanBypassTenantIsolation(ClaimsPrincipal principal)
    {
        return PortalRoles.HasRole(principal, PortalRoles.SuperAdmin) ||
               PortalRoles.HasRole(principal, PortalRoles.Admin);
    }
}
