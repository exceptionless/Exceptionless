#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Specialized;
using System.Net.Http;
using System.Security.Principal;
using System.Web.Http.Controllers;
using CodeSmith.Core.Dependency;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
//using Exceptionless.Membership;
using Exceptionless.Models;
using NLog.Fluent;

namespace Exceptionless.Core.Authorization {
    public class ExceptionlessAuthorizeAttribute : BasicHttpAuthorizeAttribute {
        [Inject]
        public IDependencyResolver Resolver { get; set; }

        protected override bool IsAuthorized(HttpActionContext context) {
            if (base.IsAuthorized(context))
                return true;

            // check query string parameter
            NameValueCollection parameters = context.Request.RequestUri.ParseQueryString();
            if (String.IsNullOrEmpty(parameters["apikey"]))
                return false;

            IPrincipal principal;
            if (!TryCreatePrincipal("client", parameters["apikey"], out principal))
                return false;

            context.Request.SetUserPrincipal(principal);
            CheckForActionOverride(context);

            return IsPrincipalAllowed(principal);
        }

        protected override bool TryCreatePrincipal(string emailAddress, string password, out IPrincipal principal) {
            principal = null;

            if (String.IsNullOrEmpty(password)) {
                Log.Error().Message("The password \"{0}\" is invalid.", password).Write();
                return false;
            }

            if (String.Equals(emailAddress, "client", StringComparison.OrdinalIgnoreCase)) {
                var projectRepository = Resolver.GetService<IProjectRepository>();
                if (projectRepository == null) {
                    Log.Error().Message("Unable to resolve IProjectRepository").Write();
                    return false;
                }

                Project project = projectRepository.GetByApiKey(password);
                if (project == null) {
                    Log.Error().Message("Unable to find a project with the Api Key: \"{0}\".", password).Write();
                    return false;
                }

                var organizationRepository = Resolver.GetService<IOrganizationRepository>();
                if (organizationRepository == null) {
                    Log.Error().Message("Unable to resolve IOrganizationRepository").Write();
                    return false;
                }

                var organization = organizationRepository.GetByIdCached(project.OrganizationId);
                if (organization == null) {
                    Log.Error().Message("Unable to find organization: \"{0}\".", project.OrganizationId).Write();
                    return false;
                }

                if (organization.IsSuspended) {
                    Log.Error().Message("Rejecting authentication because the organization \"{0}\" is suspended.", project.OrganizationId).Write();
                    AuthDeniedReason = "Account has been suspended.";
                    return false;
                }

                principal = new ExceptionlessPrincipal(project);

                return true;
            }

            var userRepository = Resolver.GetService<IUserRepository>();
            if (userRepository == null) {
                Log.Error().Message("Unable to resolve IUserRepository").Write();
                return false;
            }

            User user = userRepository.GetByEmailAddress(emailAddress);
            if (user == null) {
                Log.Error().Message("Unable to find user a with the email address: \"{0}\".", emailAddress).Write();
                return false;
            }

            //var membershipSecurity = Resolver.GetService<IMembershipSecurity>();
            //if (membershipSecurity == null) {
            //    Log.Error().Message("Unable to resolve IMembershipSecurity").Write();
            //    return false;
            //}

            if (!String.Equals(user.Password, password)) { //membershipSecurity.GetSaltedHash(password, user.Salt))) {
                Log.Error().Message("Invalid password").Write();
                return false;
            }

            principal = new ExceptionlessPrincipal(user);

            return true;
        }
    }
}