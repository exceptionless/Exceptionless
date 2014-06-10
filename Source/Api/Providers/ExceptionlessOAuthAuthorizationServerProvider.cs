using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Microsoft.Owin.Security.OAuth;

namespace Exceptionless.Api.Providers {
    public class ExceptionlessOAuthAuthorizationServerProvider : OAuthAuthorizationServerProvider {
        private readonly ITokenRepository _tokenRepository;
        private readonly IUserRepository _userRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IApplicationRepository _applicationRepository;
        public const string PublicClientId = "ex";

        public ExceptionlessOAuthAuthorizationServerProvider(ITokenRepository tokenRepository, IApplicationRepository applicationRepository, IUserRepository userRepository, IProjectRepository projectRepository) {
            _tokenRepository = tokenRepository;
            _applicationRepository = applicationRepository;
            _userRepository = userRepository;
            _projectRepository = projectRepository;
        }

        public override Task ValidateClientAuthentication(OAuthValidateClientAuthenticationContext context) {
            string clientId, clientSecret;
            if (!context.TryGetBasicCredentials(out clientId, out clientSecret))
                context.TryGetFormCredentials(out clientId, out clientSecret);
            
            clientId = clientId ?? context.ClientId;
            
            // TODO: Client id shouldn't always be required, but it may need to be on certain grant types.
            // if we have a client id, make sure that it is valid.
            if (!String.IsNullOrEmpty(clientId)) {
                var app = _applicationRepository.GetById(clientId, true);
                // if we can't find the app or a secret was specified and it does not match, then reject.
                if (app == null
                    || (!String.IsNullOrEmpty(clientSecret) && app.Secret != clientSecret))
                    return Task.FromResult(0);
            }

            context.Validated(clientId);
            return Task.FromResult(0);
        }

        public override Task GrantRefreshToken(OAuthGrantRefreshTokenContext context) {
            return base.GrantRefreshToken(context);
        }

        public override Task GrantAuthorizationCode(OAuthGrantAuthorizationCodeContext context) {
            return base.GrantAuthorizationCode(context);
        }

        public override Task GrantResourceOwnerCredentials(OAuthGrantResourceOwnerCredentialsContext context) {
            var user = _userRepository.GetByEmailAddress(context.UserName);
            // TODO: Need to verify password here.
            if (user == null)
                return Task.FromResult(0);
            
            context.Validated(user.ToIdentity(_projectRepository));

            return Task.FromResult(0);
        }

        public override Task ValidateClientRedirectUri(OAuthValidateClientRedirectUriContext context) {
            if (!String.IsNullOrEmpty(context.ClientId) && context.ClientId != PublicClientId) {
                var app = _applicationRepository.GetById(context.ClientId, true);
                if (app == null)
                    return Task.FromResult(0);

                if (!context.Request.Uri.AbsoluteUri.StartsWith(app.Url))
                    return Task.FromResult(0);

                context.Validated(context.Request.Uri.AbsoluteUri);
            } else {
                if (!context.Request.Uri.AbsoluteUri.StartsWith(context.Request.Uri.Authority))
                    return Task.FromResult(0);

                context.Validated(context.Request.Uri.AbsoluteUri);
            }

            return Task.FromResult(0);
        }
    }
}