using System;
using System.Threading.Tasks;
using Microsoft.Owin.Security.OAuth;

namespace Exceptionless.Api.Providers {
    public class ExceptionlessOAuthProvider : OAuthAuthorizationServerProvider {
        private readonly string _publicClientId;

        public ExceptionlessOAuthProvider(string publicClientId) {
            if (publicClientId == null) {
                throw new ArgumentNullException("publicClientId");
            }

            _publicClientId = publicClientId;
        }

        public override Task ValidateClientAuthentication(OAuthValidateClientAuthenticationContext context) {
            context.Validated("blah");
            return Task.FromResult(0);
        }

        public override Task ValidateClientRedirectUri(OAuthValidateClientRedirectUriContext context) {
            if (context.ClientId == _publicClientId) {
                Uri expectedRootUri = new Uri(context.Request.Uri, "/");

                if (expectedRootUri.AbsoluteUri == context.RedirectUri) {
                    context.Validated();
                } else if (context.ClientId == "web") {
                    var expectedUri = new Uri(context.Request.Uri, "/");
                    context.Validated(expectedUri.AbsoluteUri);
                }
            }

            return Task.FromResult<object>(null);
        }
    }
}