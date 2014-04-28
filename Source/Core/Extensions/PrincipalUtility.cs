using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using Exceptionless.Core.Authorization;

namespace Exceptionless.Core.Extensions {
    public static class PrincipalUtility {
        public const string ApiKeyAuthenticationType = "ApiKey";
        public const string UserAuthenticationType = "User";

        public static ClaimsPrincipal CreateClientUser(string projectId) {
            var claims = new List<Claim> {
                    new Claim(ClaimTypes.Name, projectId),
                    new Claim(ClaimTypes.Role, AuthorizationRoles.Client),
                };

            var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationType);

            return new ClaimsPrincipal(identity);
        }

        public static ClaimsPrincipal CreateUser(string name, IEnumerable<string> roles) {
            var claims = new List<Claim> {
                    new Claim(ClaimTypes.Name, name),
                    new Claim(ClaimTypes.Role, AuthorizationRoles.User)
                };

            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var identity = new ClaimsIdentity(claims, UserAuthenticationType);

            return new ClaimsPrincipal(identity);
        }

        public static bool IsApiKeyUser(this IPrincipal principal) {
            var identity = principal.GetClaimsIdentity();
            if (identity == null)
                return false;

            if (identity.AuthenticationType != ApiKeyAuthenticationType)
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

        public static string GetApiKeyProjectId(this IPrincipal principal) {
            var identity = principal.GetClaimsIdentity();
            if (identity == null)
                return null;

            if (identity.AuthenticationType != ApiKeyAuthenticationType)
                return null;

            return identity.Name;
        }
    }
}
