using Joonasw.AspNetCore.SecurityHeaders.Csp.Builder;

namespace Exceptionless.Web.Security;

internal static class ApiContentSecurityPolicy
{
    public static void AllowConfiguredOrigins(CspBuilder csp, string? apiUrl)
    {
        foreach (string origin in GetConfiguredOrigins(apiUrl))
            csp.AllowConnections.To(origin);
    }

    internal static string[] GetConfiguredOrigins(string? apiUrl)
    {
        // Accept only an administrator-configured HTTP origin, never request or forwarded headers.
        if (!Uri.TryCreate(apiUrl?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !String.IsNullOrEmpty(uri.UserInfo) ||
            uri.Host.Contains('*') ||
            uri.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6))
            return [];

        var origin = new UriBuilder(uri) { Host = uri.IdnHost, Path = String.Empty, Query = String.Empty, Fragment = String.Empty };
        string httpOrigin = origin.Uri.GetLeftPart(UriPartial.Authority);
        origin.Scheme = uri.Scheme == "https" ? "wss" : "ws";
        return [httpOrigin, origin.Uri.GetLeftPart(UriPartial.Authority)];
    }
}
