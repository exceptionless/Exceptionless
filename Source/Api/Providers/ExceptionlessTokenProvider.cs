using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Infrastructure;

namespace Exceptionless.Api {
    public class ExceptionlessTokenProvider : IAuthenticationTokenProvider {
        public void Create(AuthenticationTokenCreateContext context) {
            context.SetToken(Guid.NewGuid().ToString("N"));
        }

        public Task CreateAsync(AuthenticationTokenCreateContext context) {
            Create(context);
            return Task.FromResult(0);
        }

        public void Receive(AuthenticationTokenReceiveContext context) {
            context.SetTicket(CreateTicket());
        }

        public Task ReceiveAsync(AuthenticationTokenReceiveContext context) {
            Receive(context);
            return Task.FromResult(0);
        }

        private AuthenticationTicket CreateTicket() {
            var props = new AuthenticationProperties {
                ExpiresUtc = DateTime.UtcNow.AddDays(1),
                IssuedUtc = DateTime.UtcNow,
                IsPersistent = true
            };
            props.Dictionary["client_id"] = "blah";

            return new AuthenticationTicket(PrincipalUtility.CreateUserIdentity("eric@codesmithtools.com", new[] { "admin" }), props);    
        }
    }
}