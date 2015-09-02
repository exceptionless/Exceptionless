using System;
using System.Security.Claims;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Admin;

namespace Exceptionless.Api.Security {
    public class TokenManager {
        private readonly IUserRepository _userRepository;
        private readonly ITokenRepository _tokenRepository;
            
        public TokenManager(IUserRepository userRepository, ITokenRepository tokenRepository) {
            _userRepository = userRepository;
            _tokenRepository = tokenRepository;
        }

        public Token Create(User user) {
            var token = new Token {
                Id = StringExtensions.GetNewToken(),
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
