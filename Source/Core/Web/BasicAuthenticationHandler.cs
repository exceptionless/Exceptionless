#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Net;
using System.Net.Http;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Membership;
using NLog.Fluent;

namespace Exceptionless.Core.Web {
    public sealed class BasicAuthenticationHandler : DelegatingHandler {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMembershipSecurity _membershipSecurity;

        public BasicAuthenticationHandler(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IUserRepository userRepository, IMembershipSecurity membershipSecurity) {
            if (organizationRepository == null)
                throw new ArgumentNullException("organizationRepository");

            if (projectRepository == null)
                throw new ArgumentNullException("projectRepository");

            if (userRepository == null)
                throw new ArgumentNullException("userRepository");

            if (membershipSecurity == null)
                throw new ArgumentNullException("membershipSecurity");

            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _userRepository = userRepository;
            _membershipSecurity = membershipSecurity;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            string userName, password;
            if (!request.TryGetLoginInformation(out userName, out password)) {
                try {
                    // check query string parameter
                    var parameters = request.RequestUri.ParseQueryString();
                    if (!String.IsNullOrEmpty(parameters["apikey"])) {
                        userName = "client";
                        password = parameters["apikey"];
                    }
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("An error occurred while parsing the query string for the api key.").Write();
                }
            }

            if (!String.IsNullOrEmpty(userName) && !String.IsNullOrEmpty(password)) {
                IPrincipal principal;
                if (TryCreatePrincipal(userName, password, out principal))
                    request.SetUserPrincipal(principal);
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && !response.Headers.Contains(HttpResponseHeader.WwwAuthenticate.ToString()))
                response.Headers.Add(HttpResponseHeader.WwwAuthenticate.ToString(), ExceptionlessHeaders.Basic);

            return response;
        }

        private bool TryCreatePrincipal(string emailAddress, string password, out IPrincipal principal) {
            principal = null;

            if (String.IsNullOrEmpty(password)) {
                Log.Info().Message("The password \"{0}\" is invalid.", password).Write();
                return false;
            }

            if (String.Equals(emailAddress, "client", StringComparison.OrdinalIgnoreCase)) {
                var project = _projectRepository.GetByApiKey(password);
                if (project == null) {
                    Log.Info().Message("Unable to find a project with the Api Key: \"{0}\".", password).Write();
                    return false;
                }

                var organization = _organizationRepository.GetByIdCached(project.OrganizationId);
                if (organization == null) {
                    Log.Info().Message("Unable to find organization: \"{0}\".", project.OrganizationId).Write();
                    return false;
                }

                if (organization.IsSuspended) {
                    Log.Info().Message("Rejecting authentication because the organization \"{0}\" is suspended.", project.OrganizationId).Write();
                    return false;
                }

                principal = new ExceptionlessPrincipal(project);

                return true;
            }

            var user = _userRepository.GetByEmailAddress(emailAddress);
            if (user == null) {
                Log.Info().Message("Unable to find user a with the email address: \"{0}\".", emailAddress).Write();
                return false;
            }

            if (!String.Equals(user.Password, _membershipSecurity.GetSaltedHash(password, user.Salt))) {
                Log.Info().Message("Invalid password").Write();
                return false;
            }

            principal = new ExceptionlessPrincipal(user);

            return true;
        }
    }
}