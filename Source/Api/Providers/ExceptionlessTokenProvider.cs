using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Models.Admin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Infrastructure;

namespace Exceptionless.Api {
    public class ExceptionlessTokenProvider : AuthenticationTokenProvider {
        private readonly ITokenRepository _tokenRepository;
        private readonly IUserRepository _userRepository;

        public ExceptionlessTokenProvider(ITokenRepository tokenRepository, IUserRepository userRepository) {
            _tokenRepository = tokenRepository;
            _userRepository = userRepository;
        }

        public override void Create(AuthenticationTokenCreateContext context) {
            context.SetToken(Guid.NewGuid().ToString("N"));
        }

        public override Task CreateAsync(AuthenticationTokenCreateContext context) {
            Create(context);
            return Task.FromResult(0);
        }

        public override void Receive(AuthenticationTokenReceiveContext context) {
            var token = _tokenRepository.GetById(context.Token);
            if (token == null)
                return;

            context.SetTicket(CreateTicket(token));
        }

        public override Task ReceiveAsync(AuthenticationTokenReceiveContext context) {
            Receive(context);
            return Task.FromResult(0);
        }

        private AuthenticationTicket CreateTicket(Token token) {
            var props = new AuthenticationProperties {
                ExpiresUtc = token.ExpiresUtc,
                IssuedUtc = token.CreatedUtc,
                IsPersistent = true
            };
            props.Dictionary["client_id"] = token.ApplicationId;

            return new AuthenticationTicket(token.ToIdentity(_userRepository), props);    
        }
    }
}