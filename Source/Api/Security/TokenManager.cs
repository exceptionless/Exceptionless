using System;
using System.Security.Claims;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using Exceptionless.Models.Admin;

namespace Exceptionless.Api.Security {
    public class TokenManager {
        private readonly IUserRepository _userRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly SecurityEncoder _encoder;
            
        public TokenManager(IUserRepository userRepository, ITokenRepository tokenRepository, SecurityEncoder encoder) {
            _userRepository = userRepository;
            _tokenRepository = tokenRepository;
            _encoder = encoder;
        }

        public Token Create(User user) {
            var token = new Token {
                Id = _encoder.GetNewToken(),
                UserId = user.Id,
                CreatedUtc = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow,
                CreatedBy = user.Id,
                Type = TokenType.Access
            };
            _tokenRepository.Add(token);

            return token;
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
