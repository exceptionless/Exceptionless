using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface ITokenRepository : IRepositoryOwnedByOrganizationAndProject<Token> {
        Task<IFindResults<Token>> GetByUserIdAsync(string userId);
        Task<IFindResults<Token>> GetByTypeAndOrganizationIdAsync(TokenType type, string organizationId, PagingOptions paging = null);
        Task<IFindResults<Token>> GetByTypeAndProjectIdAsync(TokenType type, string projectId, PagingOptions paging = null);
    }
}