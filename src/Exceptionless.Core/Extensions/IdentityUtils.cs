using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using IIdentity = System.Security.Principal.IIdentity;

namespace Exceptionless.Core.Extensions {
    public static class IdentityUtils {
        public const string TokenAuthenticationType = "Token";
        public const string UserAuthenticationType = "User";
        public const string LoggedInUsersTokenId = "LoggedInUsersTokenId";
        public const string OrganizationIdsClaim = "OrganizationIds";
        public const string ProjectIdClaim = "ProjectId";
        public const string DefaultProjectIdClaim = "DefaultProjectId";

        public static ClaimsIdentity ToIdentity(this Token token) {
            if (token == null || token.Type != TokenType.Access)
                return new ClaimsIdentity();

            if (!String.IsNullOrEmpty(token.UserId))
                throw new ApplicationException("Can't create token type identity for user token.");

            var claims = new List<Claim>(5 + token.Scopes.Count) {
                new Claim(ClaimTypes.NameIdentifier, token.Id),
                new Claim(OrganizationIdsClaim, token.OrganizationId)
            };

            if (!String.IsNullOrEmpty(token.ProjectId))
                claims.Add(new Claim(ProjectIdClaim, token.ProjectId));

            if (!String.IsNullOrEmpty(token.DefaultProjectId))
                claims.Add(new Claim(DefaultProjectIdClaim, token.DefaultProjectId));

            if (token.Scopes.Count > 0) {
                foreach (string scope in token.Scopes)
                    claims.Add(new Claim(ClaimTypes.Role, scope));
            } else {
                claims.Add(new Claim(ClaimTypes.Role, AuthorizationRoles.Client));
            }

            return new ClaimsIdentity(claims, TokenAuthenticationType);
        }

        public static ClaimsIdentity ToIdentity(this User user, Token token = null) {
            if (user == null)
                return new ClaimsIdentity();

            var claims = new List<Claim>(7 + user.Roles.Count) {
                    new Claim(ClaimTypes.Name, user.EmailAddress),
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(OrganizationIdsClaim, String.Join(",", user.OrganizationIds))
                };

            if (token != null) {
                claims.Add(new Claim(LoggedInUsersTokenId, token.Id));

                if (!String.IsNullOrEmpty(token.DefaultProjectId))
                    claims.Add(new Claim(DefaultProjectIdClaim, token.DefaultProjectId));
            }

            if (user.Roles.Count > 0) {
                // add implied scopes
                if (user.Roles.Contains(AuthorizationRoles.GlobalAdmin))
                    user.Roles.Add(AuthorizationRoles.User);

                if (user.Roles.Contains(AuthorizationRoles.User))
                    user.Roles.Add(AuthorizationRoles.Client);

                foreach (string role in user.Roles)
                    claims.Add(new Claim(ClaimTypes.Role, role));
            } else {
                claims.Add(new Claim(ClaimTypes.Role, AuthorizationRoles.Client));
                claims.Add(new Claim(ClaimTypes.Role, AuthorizationRoles.User));
            }

            return new ClaimsIdentity(claims, UserAuthenticationType);
        }

        public static AuthType GetAuthType(this ClaimsPrincipal principal) {
            if (principal?.Identity == null || !principal.Identity.IsAuthenticated)
                return AuthType.Anonymous;

            return IsTokenAuthType(principal) ? AuthType.Token : AuthType.User;
        }

        public static bool IsTokenAuthType(this ClaimsPrincipal principal) {
            var identity = GetClaimsIdentity(principal);
            if (identity == null)
                return false;

            return identity.AuthenticationType == TokenAuthenticationType;
        }

        public static bool IsUserAuthType(this ClaimsPrincipal principal) {
            var identity = GetClaimsIdentity(principal);
            if (identity == null)
                return false;

            return identity.AuthenticationType == UserAuthenticationType;
        }

        public static ClaimsIdentity GetClaimsIdentity(this ClaimsPrincipal principal) {
            return principal?.Identity as ClaimsIdentity;
        }

        public static string GetUserId(this ClaimsPrincipal principal) {
            return IsUserAuthType(principal) ? GetClaimValue(principal, ClaimTypes.NameIdentifier) : null;
        }

        /// <summary>
        /// Gets the token id that authenticated the current user. If null, user logged in via oauth.
        /// </summary>
        /// <param name="principal"></param>
        /// <returns></returns>
        public static string GetLoggedInUsersTokenId(this ClaimsPrincipal principal) {
            return IsUserAuthType(principal) ? GetClaimValue(principal, LoggedInUsersTokenId) : null;
        }

        public static string GetTokenOrganizationId(this ClaimsPrincipal principal) {
            return GetClaimValue(principal, OrganizationIdsClaim);
        }

        public static string[] GetOrganizationIds(this ClaimsPrincipal principal) {
            string ids = GetClaimValue(principal, OrganizationIdsClaim);
            if (String.IsNullOrEmpty(ids))
                return new string[] { };

            return ids.Split(new [] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string GetProjectId(this ClaimsPrincipal principal) {
            return GetClaimValue(principal, ProjectIdClaim);
        }

        public static string GetDefaultProjectId(this ClaimsPrincipal principal) {
            // if this claim is for a specific project, then that is always the default project.
            return GetClaimValue(principal, ProjectIdClaim) ?? GetClaimValue(principal, DefaultProjectIdClaim);
        }

        public static string GetClaimValue(this ClaimsPrincipal principal, string type) {
            var identity = principal?.GetClaimsIdentity();
            if (identity == null)
                return null;

            return GetClaimValue(identity, type);
        }

        public static string GetClaimValue(this IIdentity identity, string type) {
            if (!(identity is ClaimsIdentity claimsIdentity))
                return null;

            var claim = claimsIdentity.FindAll(type).FirstOrDefault();
            return claim?.Value;
        }
    }

    public enum AuthType {
        User,
        Token,
        Anonymous
    }
}
