using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface ITokenRepository : IRepositoryOwnedByOrganizationAndProject<Token> {
        Task<QueryResults<Token>> GetByTypeAndUserIdAsync(TokenType type, string userId, CommandOptionsDescriptor<Token> options = null);
        Task<QueryResults<Token>> GetByTypeAndOrganizationIdAsync(TokenType type, string organizationId, CommandOptionsDescriptor<Token> options = null);
        Task<QueryResults<Token>> GetByTypeAndProjectIdAsync(TokenType type, string projectId, CommandOptionsDescriptor<Token> options = null);
        Task<long> RemoveAllByUserIdAsync(string userId, CommandOptionsDescriptor<Token> options = null);
    }
}