using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using Exceptionless.Core.Authorization;

namespace Exceptionless.Core.Extensions {
    public static class PrincipalUtility {
        public const string ProjectAuthenticationType = "Project";
        public const string UserAuthenticationType = "User";

        public static ClaimsPrincipal CreateClientUser(string projectId) {
            var claims = new List<Claim> {
                    new Claim(ClaimTypes.Name, projectId),
                    new Claim(ClaimTypes.Role, AuthorizationRoles.Client),
                };

            var identity = new ClaimsIdentity(claims, ProjectAuthenticationType);

            return new ClaimsPrincipal(identity);
        }

        public static ClaimsPrincipal CreateUser(string userId, IEnumerable<string> roles) {
            var claims = new List<Claim> {
                    new Claim(ClaimTypes.Name, userId),
                    new Claim(ClaimTypes.Role, AuthorizationRoles.User)
                };

            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var identity = new ClaimsIdentity(claims, UserAuthenticationType);

            return new ClaimsPrincipal(identity);
        }

        public static AuthType GetAuthType(this IPrincipal principal) {
            if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated)
                return AuthType.Anonymous;

            return IsProjectAuthType(principal) ? AuthType.Project : AuthType.User;
        }

        public static bool IsProjectAuthType(this IPrincipal principal) {
            var identity = principal.GetClaimsIdentity();
            if (identity == null)
                return false;

            if (identity.AuthenticationType != ProjectAuthenticationType)
                return false;

            return true;
        }

        public static bool IsUserAuthType(this IPrincipal principal) {
            var identity = principal.GetClaimsIdentity();
            if (identity == null)
                return false;

            if (identity.AuthenticationType != UserAuthenticationType)
                return false;

            return true;
        }

        public static ClaimsPrincipal GetClaimsPrincipal(this IPrincipal principal) {
            return principal as ClaimsPrincipal;
        }

        public static ClaimsIdentity GetClaimsIdentity(this IPrincipal principal) {
            var claimsPrincipal = principal.GetClaimsPrincipal();
            if (claimsPrincipal == null)
                return null;

            var identity = claimsPrincipal.Identity as ClaimsIdentity;
            if (identity == null)
                return null;

            return identity;
        }

        public static string GetProjectId(this IPrincipal principal) {
            var identity = principal.GetClaimsIdentity();
            if (identity == null)
                return null;

            if (identity.AuthenticationType != ProjectAuthenticationType)
                return null;

            return identity.Name;
        }

        public static string GetUserId(this IPrincipal principal) {
            var identity = principal.GetClaimsIdentity();
            if (identity == null)
                return null;

            if (identity.AuthenticationType != UserAuthenticationType)
                return null;

            return identity.Name;
        }
    }
    
    public enum AuthType {
        User,
        Project,
        Anonymous
    }
}
