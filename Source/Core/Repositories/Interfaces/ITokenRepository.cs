using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface ITokenRepository : IRepositoryOwnedByOrganizationAndProject<Token> {
        Task<Token> GetByRefreshTokenAsync(string refreshToken);

        Task<FindResults<Token>> GetApiTokensAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);

        Task<FindResults<Token>> GetByUserIdAsync(string userId);

        Task<FindResults<Token>> GetByTypeAndOrganizationIdAsync(TokenType type, string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);

        Task<FindResults<Token>> GetByTypeAndOrganizationIdsAsync(TokenType type, ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);

        Task<FindResults<Token>> GetByTypeAndProjectIdAsync(TokenType type, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);

        Task<FindResults<Token>> GetByTypeAndOrganizationIdOrProjectIdAsync(TokenType type, string organizationId, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);
    }
}