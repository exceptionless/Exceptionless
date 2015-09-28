using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;

namespace Exceptionless.Api.Security {
    public class TokenManager {
        private readonly ITokenRepository _tokenRepository;
            
        public TokenManager(ITokenRepository tokenRepository) {
            _tokenRepository = tokenRepository;
        }

        public async Task<Token> GetOrCreateAsync(User user) {
            var existingToken = (await _tokenRepository.GetByUserIdAsync(user.Id)).Documents.FirstOrDefault(t => t.ExpiresUtc > DateTime.UtcNow && t.Type == TokenType.Access);
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

            await _tokenRepository.AddAsync(token);

            return token;
        }
    }
}
