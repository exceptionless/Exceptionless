using System;
using System.Security.Claims;
using Exceptionless.Api.Models;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using Exceptionless.Models.Admin;

namespace Exceptionless.Api.Security {
    public class TokenManager {
        private readonly IUserRepository _userRepository;
        private readonly ITokenRepository _tokenRepository;

        public TokenManager(IUserRepository userRepository, ITokenRepository tokenRepository) {
            _userRepository = userRepository;
            _tokenRepository = tokenRepository;
        }

        public Token Create(User actingUser, NewToken request) {
            return null;
        }

        public ClaimsPrincipal Validate(string token) {
            var tokenRecord = _tokenRepository.GetById(token, true);
            if (tokenRecord == null)
                return null;

            if (tokenRecord.ExpiresUtc.HasValue && tokenRecord.ExpiresUtc.Value < DateTime.UtcNow)
                return null;

            var principal = new ClaimsPrincipal(tokenRecord.ToIdentity(_userRepository));
            return principal;  
        }
    }
}
