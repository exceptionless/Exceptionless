using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface ITokenRepository : IRepositoryOwnedByOrganizationAndProject<Token> {
        Token GetByRefreshToken(string refreshToken);

        FindResults<Token> GetApiTokens(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);

        FindResults<Token> GetByUserId(string userId);

        FindResults<Token> GetByTypeAndOrganizationId(TokenType type, string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);

        FindResults<Token> GetByTypeAndOrganizationIds(TokenType type, ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);

        FindResults<Token> GetByTypeAndProjectId(TokenType type, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);

        FindResults<Token> GetByTypeAndOrganizationIdOrProjectId(TokenType type, string organizationId, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);
    }
}