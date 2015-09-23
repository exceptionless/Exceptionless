using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Microsoft.Owin;
using Nito.AsyncEx;

namespace Exceptionless.Api.Extensions {
    public static class HttpExtensions {
        public static async Task<User> GetUserAsync(this HttpRequestMessage message) {
            var user = message?.GetOwinContext().Get<AsyncLazy<User>>("User");
            if (user != null)
                return await user;

            return null;
        }

        public static async Task<User> GetUserAsync(this IOwinRequest request) {
            var user = request?.Context.Get<AsyncLazy<User>>("User");
            if (user != null)
                return await user;

            return null;
        }

        public static async Task<Project> GetDefaultProjectAsync(this HttpRequestMessage message) {
            var project = message?.GetOwinContext().Get<AsyncLazy<Project>>("DefaultProject");
            if (project != null)
                return await project;

            return null;
        }

        public static async Task<string> GetDefaultProjectIdAsync(this HttpRequestMessage message) {
            if (message == null)
                return null;

            var project = await message.GetDefaultProjectAsync();
            if (project != null)
                return project.Id;

            return message.GetClaimsPrincipal().GetDefaultProjectId();
        }

        public static ClaimsPrincipal GetClaimsPrincipal(this HttpRequestMessage message) {
            var context = message?.GetOwinContext();
            return context?.Request?.User?.GetClaimsPrincipal();
        }

        public static AuthType GetAuthType(this HttpRequestMessage message) {
            if (message == null)
                return AuthType.Anonymous;

            var principal = message.GetClaimsPrincipal();
            return principal?.GetAuthType() ?? AuthType.Anonymous;
        }

        public static async Task<bool> CanAccessOrganizationAsync(this HttpRequestMessage message, string organizationId) {
            if (message == null)
                return false;

            if (await message.IsInOrganizationAsync(organizationId))
                return true;

            return message.IsGlobalAdmin();
        }

        public static bool IsGlobalAdmin(this HttpRequestMessage message) {
            if (message == null)
                return false;

            var principal = message.GetClaimsPrincipal();
            return principal != null && principal.IsInRole(AuthorizationRoles.GlobalAdmin);
        }

        public static async Task<bool> IsInOrganizationAsync(this HttpRequestMessage message, string organizationId) {
            if (message == null)
                return false;

            if (String.IsNullOrEmpty(organizationId))
                return false;

            return (await message.GetAssociatedOrganizationIdsAsync()).Contains(organizationId);
        }

        public static async Task<ICollection<string>> GetAssociatedOrganizationIdsAsync(this HttpRequestMessage message) {
            if (message == null)
                return new List<string>();

            var user = await message.GetUserAsync();
            if (user != null)
                return user.OrganizationIds;

            var principal = message.GetClaimsPrincipal();
            return principal.GetOrganizationIds();
        }

        public static async Task<string> GetDefaultOrganizationIdAsync(this HttpRequestMessage message) {
            if (message == null)
                return null;

            // TODO: Try to figure out the 1st organization that the user owns instead of just selecting from associated orgs.
            return (await message.GetAssociatedOrganizationIdsAsync()).FirstOrDefault();
        }

        public static string GetClientIpAddress(this HttpRequestMessage request) {
            var context = request?.GetOwinContext();
            return context?.Request.RemoteIpAddress;
        }

        public static string GetQueryString(this HttpRequestMessage request, string key) {
            var queryStrings = request?.GetQueryNameValuePairs();
            if (queryStrings == null)
                return null;

            var match = queryStrings.FirstOrDefault(kv => kv.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (String.IsNullOrEmpty(match.Value))
                return null;

            return match.Value;
        }

        public static string GetCookie(this HttpRequestMessage request, string cookieName) {
            CookieHeaderValue cookie = request?.Headers.GetCookies(cookieName).FirstOrDefault();
            return cookie?[cookieName].Value;
        }

        public static AuthInfo GetBasicAuth(this HttpRequestMessage request) {
            var authHeader = request?.Headers.Authorization;

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