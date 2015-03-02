using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Admin;
using IIdentity = System.Security.Principal.IIdentity;

namespace Exceptionless.Core.Extensions {
    public static class IdentityUtils {
        public const string TokenAuthenticationType = "Token";
        public const string UserAuthenticationType = "User";
        public const string UserIdClaim = "UserId";
        public const string OrganizationIdsClaim = "OrganizationIds";
        public const string ProjectIdClaim = "ProjectId";
        public const string DefaultProjectIdClaim = "DefaultProjectId";

        public static ClaimsIdentity ToIdentity(this Token token, IUserRepository userRepository) {
            if (token == null || token.Type != TokenType.Access)
                return WindowsIdentity.GetAnonymous();

            if (!String.IsNullOrEmpty(token.UserId))
                return CreateUserIdentity(token.UserId, userRepository, token.ProjectId ?? token.DefaultProjectId);

            var claims = new List<Claim> {
                new Claim(ClaimTypes.NameIdentifier, token.Id),
                new Claim(OrganizationIdsClaim, token.OrganizationId)
            };

            if (!String.IsNullOrEmpty(token.ProjectId))
                claims.Add(new Claim(ProjectIdClaim, token.ProjectId));

            if (!String.IsNullOrEmpty(token.DefaultProjectId))
                claims.Add(new Claim(DefaultProjectIdClaim, token.DefaultProjectId));

            if (token.Scopes.Count > 0)
                claims.AddRange(token.Scopes.Select(scope => new Claim(ClaimTypes.Role, scope)));
            else
                claims.Add(new Claim(ClaimTypes.Role, Authorization.AuthorizationRoles.Client));

            return new ClaimsIdentity(claims, TokenAuthenticationType);
        }

        public static ClaimsIdentity CreateUserIdentity(string userId, IUserRepository userRepository, string defaultProjectId = null) {
            if (String.IsNullOrEmpty(userId))
                throw new ArgumentNullException("userId");
            if (userRepository == null)
                throw new ArgumentNullException("userRepository");

            var user = userRepository.GetById(userId, true);
            if (user == null)
                return WindowsIdentity.GetAnonymous();
            
            return CreateUserIdentity(user.EmailAddress, user.Id, user.OrganizationIds.ToArray(), user.Roles.ToArray(), defaultProjectId);
        }

        public static ClaimsIdentity ToIdentity(this User user, string defaultProjectId = null) {
            if (user == null)
                return WindowsIdentity.GetAnonymous();

            return CreateUserIdentity(user.EmailAddress, user.Id, user.OrganizationIds.ToArray(), user.Roles.ToArray(), defaultProjectId);
        }

        public static ClaimsIdentity CreateUserIdentity(string emailAddress, string id, string[] organizationIds, string[] roles, string defaultProjectId = null) {
            var claims = new List<Claim> {
                    new Claim(ClaimTypes.Name, emailAddress),
                    new Claim(ClaimTypes.NameIdentifier, id),
                    new Claim(OrganizationIdsClaim, String.Join(",", organizationIds))
                };

            if (!String.IsNullOrEmpty(defaultProjectId))
                claims.Add(new Claim(DefaultProjectIdClaim, defaultProjectId));

            var userRoles = new HashSet<string>(roles);
            if (userRoles.Any()) {
                // add implied scopes
                if (userRoles.Contains(AuthorizationRoles.GlobalAdmin))
                    userRoles.Add(AuthorizationRoles.User);

                if (userRoles.Contains(AuthorizationRoles.User))
                    userRoles.Add(AuthorizationRoles.Client);

                claims.AddRange(userRoles.Select(scope => new Claim(ClaimTypes.Role, scope)));
            } else {
                claims.Add(new Claim(ClaimTypes.Role, AuthorizationRoles.Client));
                claims.Add(new Claim(ClaimTypes.Role, AuthorizationRoles.User));
            }

            return new ClaimsIdentity(claims, UserAuthenticationType);
        }

        public static AuthType GetAuthType(this IPrincipal principal) {
            if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated)
                return AuthType.Anonymous;

            return IsTokenAuthType(principal) ? AuthType.Token : AuthType.User;
        }

        public static bool IsTokenAuthType(this IPrincipal principal) {
            var identity = GetClaimsIdentity(principal);
            if (identity == null)
                return false;

            return identity.AuthenticationType == TokenAuthenticationType;
        }

        public static bool IsUserAuthType(this IPrincipal principal) {
            var identity = GetClaimsIdentity(principal);
            if (identity == null)
                return false;

            return identity.AuthenticationType == UserAuthenticationType;
        }

        public static ClaimsPrincipal GetClaimsPrincipal(this IPrincipal principal) {
            return principal as ClaimsPrincipal;
        }

        public static ClaimsIdentity GetClaimsIdentity(this IPrincipal principal) {
            var claimsPrincipal = GetClaimsPrincipal(principal);
            if (claimsPrincipal == null)
                return null;

            var identity = claimsPrincipal.Identity as ClaimsIdentity;
            if (identity == null)
                return null;

            return identity;
        }

        public static string GetUserId(this IPrincipal principal) {
            return IsTokenAuthType(principal) ? GetClaimValue(principal, UserIdClaim) : GetClaimValue(principal, ClaimTypes.NameIdentifier);
        }

        public static string[] GetOrganizationIds(this IPrincipal principal) {
            string orgIds =  GetClaimValue(principal, OrganizationIdsClaim);
            if (String.IsNullOrEmpty(orgIds))
                return new string[] { };

            return orgIds.Split(new [] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string GetProjectId(this IPrincipal principal) {
            return GetClaimValue(principal, ProjectIdClaim);
        }

        public static string GetDefaultProjectId(this IPrincipal principal) {
            // if this claim is for a specific project, then that is always the default project.
            return GetClaimValue(principal, ProjectIdClaim) ?? GetClaimValue(principal, DefaultProjectIdClaim);
        }

        public static string GetClaimValue(this IPrincipal principal, string type) {
            if (principal == null)
                return null;

            var identity = principal.GetClaimsIdentity();
            if (identity == null)
                return null;

            return GetClaimValue(identity, type);
        }

        public static string GetClaimValue(this IIdentity identity, string type) {
            var claimsIdentity = identity as ClaimsIdentity;
            if (claimsIdentity == null)
                return null;

            var claim = claimsIdentity.FindAll(type).FirstOrDefault();
            if (claim == null)
                return null;

            return claim.Value;
        }
    }
    
    public enum AuthType {
        User,
        Token,
        Anonymous
    }
}
