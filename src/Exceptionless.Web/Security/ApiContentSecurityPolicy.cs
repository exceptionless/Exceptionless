using Joonasw.AspNetCore.SecurityHeaders.Csp;
using Joonasw.AspNetCore.SecurityHeaders.Csp.Builder;

namespace Exceptionless.Web.Security;

internal static class ApiContentSecurityPolicy
{
    public static void AllowConfiguredOrigins(CspBuilder csp, string? apiUrl)
    {
        // Allow only an administrator-configured origin, never request or forwarded headers.
        if (!Uri.TryCreate(apiUrl?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !String.IsNullOrEmpty(uri.UserInfo) ||
            uri.Host.Contains('*') ||
            uri.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6))
            return;

        var origin = new UriBuilder(uri) { Host = uri.IdnHost, Path = String.Empty, Query = String.Empty, Fragment = String.Empty };
        csp.AllowConnections.To(origin.Uri.GetLeftPart(UriPartial.Authority));
        origin.Scheme = uri.Scheme == "https" ? "wss" : "ws";
        csp.AllowConnections.To(origin.Uri.GetLeftPart(UriPartial.Authority));
    }
}
