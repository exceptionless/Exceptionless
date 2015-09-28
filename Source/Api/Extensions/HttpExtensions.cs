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
using Exceptionless.Core.Repositories;

namespace Exceptionless.Api.Extensions {
    public static class HttpExtensions {
        public static User GetUser(this HttpRequestMessage message) {
            return message?.GetOwinContext().Get<User>("User");
        }
        
        public static void SetUser(this HttpRequestMessage message, User user) {
            message?.GetOwinContext().Set("User", user);
        }

        public static Project GetProject(this HttpRequestMessage message) {
            return message?.GetOwinContext().Get<Project>("Project");
        }

        public static void SetProject(this HttpRequestMessage message, Project project) {
            message?.GetOwinContext().Set("Project", project);
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

        public static bool CanAccessOrganization(this HttpRequestMessage message, string organizationId) {
            if (message == null)
                return false;

            if (message.IsInOrganization(organizationId))
                return true;

            return message.IsGlobalAdmin();
        }

        public static bool IsGlobalAdmin(this HttpRequestMessage message) {
            if (message == null)
                return false;

            var principal = message.GetClaimsPrincipal();
            return principal != null && principal.IsInRole(AuthorizationRoles.GlobalAdmin);
        }

        public static bool IsInOrganization(this HttpRequestMessage message, string organizationId) {
            if (message == null)
                return false;

            if (String.IsNullOrEmpty(organizationId))
                return false;

            return message.GetAssociatedOrganizationIds().Contains(organizationId);
        }

        public static ICollection<string> GetAssociatedOrganizationIds(this HttpRequestMessage message) {
            if (message == null)
                return new List<string>();

            var user = message.GetUser();
            if (user != null)
                return user.OrganizationIds;

            var principal = message.GetClaimsPrincipal();
            return principal.GetOrganizationIds();
        }

        public static string GetDefaultOrganizationId(this HttpRequestMessage message) {
            // TODO: Try to figure out the 1st organization that the user owns instead of just selecting from associated orgs.
            return message?.GetAssociatedOrganizationIds().FirstOrDefault();
        }

        public static string GetDefaultProjectId(this HttpRequestMessage message) {
            // Use project id from url. E.G., /api/v{version:int=2}/projects/{projectId:objectid}/events
            //var path = message.RequestUri.AbsolutePath;

            var principal = message.GetClaimsPrincipal();
            return principal?.GetDefaultProjectId();
        }

        public static async Task<Project> GetDefaultProjectAsync(this HttpRequestMessage message, IProjectRepository projectRepository) {
            string projectId = message.GetDefaultProjectId();
            if (String.IsNullOrEmpty(projectId)) {
                var firstOrgId = message.GetAssociatedOrganizationIds().FirstOrDefault();
                if (!String.IsNullOrEmpty(firstOrgId)) {
                    var project = (await projectRepository.GetByOrganizationIdAsync(firstOrgId, useCache: true)).Documents.FirstOrDefault();
                    if (project != null)
                        return project;
                }
            }

            if (String.IsNullOrEmpty(projectId))
                return null;

            return await projectRepository.GetByIdAsync(projectId, true);
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