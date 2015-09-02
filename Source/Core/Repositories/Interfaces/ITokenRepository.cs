using System;
using System.Collections.Generic;
using Exceptionless.Core.Models.Admin;

namespace Exceptionless.Core.Repositories {
    public interface ITokenRepository : IRepositoryOwnedByOrganizationAndProject<Token> {
        Token GetByRefreshToken(string refreshToken);

        ICollection<Token> GetByTypeAndOrganizationId(TokenType type, string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);

        ICollection<Token> GetByTypeAndOrganizationIds(TokenType type, ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);

        ICollection<Token> GetByTypeAndProjectId(TokenType type, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);

        ICollection<Token> GetByTypeAndOrganizationIdOrProjectId(TokenType type, string organizationId, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);
    }
}