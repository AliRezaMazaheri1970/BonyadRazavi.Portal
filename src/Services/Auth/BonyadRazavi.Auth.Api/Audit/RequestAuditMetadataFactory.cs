using System.Net;
using System.Security.Claims;

namespace BonyadRazavi.Auth.Api.Audit;

public static class RequestAuditMetadataFactory
{
    public static IReadOnlyDictionary<string, object?> Create(
        HttpContext context,
        IDictionary<string, object?>? customValues = null)
    {
        var ip = ResolveClientIp(context);
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var (deviceType, operatingSystem) = ParseClientEnvironment(userAgent);
        var actorUserId = ResolveAuthenticatedUserId(context.User);
        var actorUserName = context.User.FindFirstValue(ClaimTypes.Name);

        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ip"] = ip,
            ["userAgent"] = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent,
            ["device"] = deviceType,
            ["os"] = operatingSystem,
            ["actorUserId"] = actorUserId,
            ["actorUserName"] = string.IsNullOrWhiteSpace(actorUserName) ? null : actorUserName,
            ["path"] = context.Request.Path.ToString(),
            ["method"] = context.Request.Method,
            ["traceId"] = context.TraceIdentifier,
            ["utc"] = DateTime.UtcNow
        };

        if (customValues is not null)
        {
            foreach (var (key, value) in customValues)
            {
                metadata[key] = value;
            }
        }

        return metadata;
    }

    public static Guid? ResolveAuthenticatedUserId(ClaimsPrincipal? user)
    {
        if (user is null)
        {
            return null;
        }

        var userIdRawValue = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(userIdRawValue, out var userId) ? userId : null;
    }

    public static string ResolveClientIp(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedForHeader))
        {
            var forwardedIp = forwardedForHeader
                .ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedIp) && IPAddress.TryParse(forwardedIp, out _))
            {
                return forwardedIp;
            }
        }

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is not null)
        {
            if (remoteIp.IsIPv4MappedToIPv6)
            {
                remoteIp = remoteIp.MapToIPv4();
            }

            return remoteIp.ToString();
        }

        return "unknown-ip";
    }

    private static (string DeviceType, string OperatingSystem) ParseClientEnvironment(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return ("Unknown", "Unknown");
        }

        var os = "Unknown";
        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
        {
            os = "Windows";
        }
        else if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
        {
            os = "Android";
        }
        else if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
                 userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase) ||
                 userAgent.Contains("iOS", StringComparison.OrdinalIgnoreCase))
        {
            os = "iOS";
        }
        else if (userAgent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase))
        {
            os = "macOS";
        }
        else if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
        {
            os = "Linux";
        }

        var deviceType = userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase)
            ? "Mobile"
            : "Desktop";

        return (deviceType, os);
    }
}
