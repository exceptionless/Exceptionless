using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Claims;
using System.Text;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;

namespace Exceptionless.Web.Extensions;

public static class HttpExtensions
{
    public static User? GetUser(this HttpRequest request)
    {
        return request.HttpContext.Items.TryGetAndReturn("User") as User;
    }

    public static void SetUser(this HttpRequest request, User user)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        request.HttpContext.Items["User"] = user;
    }

    public static Project? GetProject(this HttpRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return request.HttpContext.Items.TryGetAndReturn("Project") as Project;
    }

    public static void SetProject(this HttpRequest request, Project project)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        request.HttpContext.Items["Project"] = project;
    }

    public static ClaimsPrincipal GetClaimsPrincipal(this HttpRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return request.HttpContext.User;
    }

    public static AuthType GetAuthType(this HttpRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var principal = request.GetClaimsPrincipal();
        return principal.GetAuthType();
    }

    public static bool CanAccessOrganization(this HttpRequest request, string organizationId)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.IsInOrganization(organizationId))
            return true;

        return request.IsGlobalAdmin();
    }

    public static bool IsGlobalAdmin(this HttpRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var principal = request.GetClaimsPrincipal();
        return principal.IsInRole(AuthorizationRoles.GlobalAdmin);
    }

    public static bool IsInOrganization(this HttpRequest request, string organizationId)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (String.IsNullOrEmpty(organizationId))
            return false;

        return request.GetAssociatedOrganizationIds().Contains(organizationId);
    }

    public static ICollection<string> GetAssociatedOrganizationIds(this HttpRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var principal = request.GetClaimsPrincipal();
        return principal.GetOrganizationIds();
    }

    public static string? GetTokenOrganizationId(this HttpRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var principal = request.GetClaimsPrincipal();
        return principal.GetTokenOrganizationId();
    }

    public static string? GetDefaultOrganizationId(this HttpRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return request.GetAssociatedOrganizationIds().FirstOrDefault();
    }

    public static string? GetProjectId(this HttpRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var principal = request.GetClaimsPrincipal();
        return principal.GetProjectId();
    }

    public static string? GetDefaultProjectId(this HttpRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        // TODO: Use project id from url. E.G., /api/v{apiVersion:int=2}/projects/{projectId:objectid}/events
        //var path = request.Path.Value;

        var principal = request.GetClaimsPrincipal();
        return principal.GetDefaultProjectId();
    }

    public static string? GetClientIpAddress(this HttpRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return request.HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    public static string? GetQueryString(this HttpRequest request, string key)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.Query.TryGetValue(key, out var queryStrings))
            return queryStrings;

        return null;
    }

    public static AuthInfo? GetBasicAuth(this HttpRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        string? authHeader = request.Headers.TryGetAndReturn("Authorization");
        if (authHeader is null || !authHeader.StartsWith("basic", StringComparison.OrdinalIgnoreCase))
            return null;

        string token = authHeader.Substring(6).Trim();
        string credentialString = Encoding.UTF8.GetString(Convert.FromBase64String(token));
        string[] credentials = credentialString.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (credentials.Length != 2)
            return null;

        return new AuthInfo
        {
            Username = credentials[0],
            Password = credentials[1]
        };
    }

    public static bool IsLocal(this HttpRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.Host.Host.Contains("localtest.me", StringComparison.OrdinalIgnoreCase) ||
            request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        var connection = request.HttpContext.Connection;

        if (IsSet(connection.RemoteIpAddress))
        {
            return IsSet(connection.LocalIpAddress)
                ? connection.RemoteIpAddress.Equals(connection.LocalIpAddress)
                : IPAddress.IsLoopback(connection.RemoteIpAddress);
        }

        return true;
    }

    private const string NullIpAddress = "::1";

    private static bool IsSet([NotNullWhen(true)] IPAddress? address)
    {
        return address is not null && address.ToString() != NullIpAddress;
    }

    public static bool IsEventPost(this HttpRequest request)
    {
        string? absolutePath = request.Path.Value;
        if (absolutePath is null)
            return false;

        string method = request.Method;
        if (String.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            return absolutePath.Contains("/events/submit");

        if (!String.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            return false;

        if (absolutePath.EndsWith("/"))
            absolutePath = absolutePath.Substring(0, absolutePath.Length - 1);

        return absolutePath.EndsWith("/events", StringComparison.OrdinalIgnoreCase)
            || String.Equals(absolutePath, "/api/v1/error", StringComparison.OrdinalIgnoreCase);
    }
}

public record AuthInfo
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}
