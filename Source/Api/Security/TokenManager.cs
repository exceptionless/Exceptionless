using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;

namespace Exceptionless.Api.Security {
    public class TokenManager {
        private readonly IUserRepository _userRepository;
        private readonly ITokenRepository _tokenRepository;
            
        public TokenManager(IUserRepository userRepository, ITokenRepository tokenRepository) {
            _userRepository = userRepository;
            _tokenRepository = tokenRepository;
        }

        public async Task<Token> GetOrCreateAsync(User user) {
            var existingToken = (await _tokenRepository.GetByUserIdAsync(user.Id).AnyContext()).Documents.FirstOrDefault(t => t.ExpiresUtc > DateTime.UtcNow && t.Type == TokenType.Access);
            if (existingToken != null)
                return existingToken;

            var token = new Token {
                Id = StringExtensions.GetNewToken(),
                UserId = user.Id,
                CreatedUtc = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow,
                CreatedBy = user.Id,
                Type = TokenType.Access
            };

            await _tokenRepository.AddAsync(token).AnyContext();

            return token;
        }

        public async Task<ClaimsPrincipal> ValidateAsync(string token) {
            var tokenRecord = await _tokenRepository.GetByIdAsync(token, true).AnyContext();
            if (tokenRecord == null)
                return null;

            if (tokenRecord.ExpiresUtc.HasValue && tokenRecord.ExpiresUtc.Value < DateTime.UtcNow)
                return null;

            var principal = new ClaimsPrincipal(await tokenRecord.ToIdentityAsync(_userRepository).AnyContext());
            return principal;  
        }
    }
}
