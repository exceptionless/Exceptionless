using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Exceptionless.Api.Extensions {
    public static class HttpExtensions {
        public static User GetUser(this HttpRequest request) {
            return request.HttpContext.Items.TryGetAndReturn("User") as User;
        }

        public static void SetUser(this HttpRequest request, User user) {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (user != null)
                request.HttpContext.Items["User"] = user;
        }

        public static Organization GetOrganization(this HttpRequest request) {
            return request?.HttpContext.Items.TryGetAndReturn("Organization") as Organization;
        }

        public static void SetOrganization(this HttpRequest request, Organization organization) {
            if (organization != null)
                request.HttpContext.Items["Organization"] = organization;
        }

        public static Project GetProject(this HttpRequest request) {
            return request?.HttpContext.Items.TryGetAndReturn("Project") as Project;
        }

        public static void SetProject(this HttpRequest request, Project project) {
            if (project != null)
                request.HttpContext.Items["Project"] = project;
        }

        public static ClaimsPrincipal GetClaimsPrincipal(this HttpRequest request) {
            return request.HttpContext.User;
        }

        public static AuthType GetAuthType(this HttpRequest request) {
            var principal = request.GetClaimsPrincipal();
            return principal.GetAuthType();
        }

        public static bool CanAccessOrganization(this HttpRequest request, string organizationId) {
            if (request.IsInOrganization(organizationId))
                return true;

            return request.IsGlobalAdmin();
        }

        public static bool IsGlobalAdmin(this HttpRequest request) {
            var principal = request.GetClaimsPrincipal();
            return principal != null && principal.IsInRole(AuthorizationRoles.GlobalAdmin);
        }

        public static bool IsInOrganization(this HttpRequest request, string organizationId) {
            if (String.IsNullOrEmpty(organizationId))
                return false;

            return request.GetAssociatedOrganizationIds().Contains(organizationId);
        }

        public static ICollection<string> GetAssociatedOrganizationIds(this HttpRequest request) {
            var principal = request.GetClaimsPrincipal();
            return principal?.GetOrganizationIds();
        }

        public static string GetTokenOrganizationId(this HttpRequest request) {
            var principal = request.GetClaimsPrincipal();
            return principal?.GetTokenOrganizationId();
        }

        public static string GetDefaultOrganizationId(this HttpRequest request) {
            return request?.GetAssociatedOrganizationIds().FirstOrDefault();
        }

        public static string GetDefaultProjectId(this HttpRequest request) {
            // TODO: Use project id from url. E.G., /api/v{apiVersion:int=2}/projects/{projectId:objectid}/events
            //var path = request.Path.Value;

            var principal = request.GetClaimsPrincipal();
            return principal?.GetDefaultProjectId();
        }

        public static string GetClientIpAddress(this HttpRequest request) {
            return request.HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        public static string GetQueryString(this HttpRequest request, string key) {
            if (request.Query.TryGetValue(key, out var queryStrings))
                return queryStrings;

            return null;
        }

        public static AuthInfo GetBasicAuth(this HttpRequest request) {
            string authHeader = request.Headers.TryGetAndReturn("Authorization");
            if (authHeader == null || !authHeader.StartsWith("basic", StringComparison.OrdinalIgnoreCase))
                return null;

            string token = authHeader.Substring(6).Trim();
            string credentialstring = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var credentials = credentialstring.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (credentials.Length != 2)
                return null;

            return new AuthInfo {
                Username = credentials[0],
                Password = credentials[1]
            };
        }
    }

    public class AuthInfo {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
