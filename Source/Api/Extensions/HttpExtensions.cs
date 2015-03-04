using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Microsoft.Owin;

namespace Exceptionless.Api.Extensions {
    public static class HttpExtensions {
        public static User GetUser(this HttpRequestMessage message) {
            if (message == null)
                return null;

            var user = message.GetOwinContext().Get<Lazy<User>>("User");
            if (user != null)
                return user.Value;

            return null;
        }

        public static User GetUser(this IOwinRequest request) {
            if (request == null)
                return null;

            var user = request.Context.Get<Lazy<User>>("User");
            if (user != null)
                return user.Value;

            return null;
        }

        public static Project GetDefaultProject(this HttpRequestMessage message) {
            if (message == null)
                return null;

            var project = message.GetOwinContext().Get<Lazy<Project>>("DefaultProject");
            if (project != null)
                return project.Value;

            return null;
        }

        public static string GetDefaultProjectId(this HttpRequestMessage message) {
            var project = message.GetDefaultProject();
            if (project != null)
                return project.Id;

            return message.GetClaimsPrincipal().GetDefaultProjectId();
        }

        public static ClaimsPrincipal GetClaimsPrincipal(this HttpRequestMessage message) {
            var context = message.GetOwinContext();
            if (context == null || context.Request == null || context.Request.User == null)
                return null;

            return context.Request.User.GetClaimsPrincipal();
        }

        public static AuthType GetAuthType(this HttpRequestMessage message) {
            var principal = message.GetClaimsPrincipal();
            return principal == null ? AuthType.Anonymous : principal.GetAuthType();
        }

        public static bool CanAccessOrganization(this HttpRequestMessage message, string organizationId) {
            if (message.IsInOrganization(organizationId))
                return true;

            return message.IsGlobalAdmin();
        }

        public static bool IsGlobalAdmin(this HttpRequestMessage message) {
            var principal = message.GetClaimsPrincipal();
            return principal != null && principal.IsInRole(AuthorizationRoles.GlobalAdmin);
        }

        public static bool IsInOrganization(this HttpRequestMessage message, string organizationId) {
            if (String.IsNullOrEmpty(organizationId))
                return false;

            return message.GetAssociatedOrganizationIds().Contains(organizationId);
        }

        public static ICollection<string> GetAssociatedOrganizationIds(this HttpRequestMessage message) {
            if (message.GetUser() != null)
                return message.GetUser().OrganizationIds;

            var principal = message.GetClaimsPrincipal();
            return principal.GetOrganizationIds();
        }

        public static string GetDefaultOrganizationId(this HttpRequestMessage message) {
            // TODO: Try to figure out the 1st organization that the user owns instead of just selecting from associated orgs.
            return message.GetAssociatedOrganizationIds().FirstOrDefault();
        }

        public static string GetClientIpAddress(this HttpRequestMessage request) {
            var context = request.GetOwinContext();
            if (context != null)
                return context.Request.RemoteIpAddress;

            return null;
        }

        public static string GetQueryString(this HttpRequestMessage request, string key) {
            var queryStrings = request.GetQueryNameValuePairs();
            if (queryStrings == null)
                return null;

            var match = queryStrings.FirstOrDefault(kv => kv.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (String.IsNullOrEmpty(match.Value))
                return null;

            return match.Value;
        }

        public static string GetCookie(this HttpRequestMessage request, string cookieName) {
            CookieHeaderValue cookie = request.Headers.GetCookies(cookieName).FirstOrDefault();
            if (cookie != null)
                return cookie[cookieName].Value;

            return null;
        }

        public static AuthInfo GetBasicAuth(this HttpRequestMessage request) {
            var authHeader = request.Headers.Authorization;

            if (authHeader == null || authHeader.Scheme.ToLower() != "basic")
                return null;

            string data = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Parameter));
            if (String.IsNullOrEmpty(data))
                return null;

            string[] authParts = data.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (authParts.Length != 2)
                return null;

            return new AuthInfo {
                Username = authParts[0],
                Password = authParts[1]
            };
        }
    }

    public class AuthInfo {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}