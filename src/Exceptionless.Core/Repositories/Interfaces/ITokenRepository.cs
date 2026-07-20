using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public interface ITokenRepository : IRepositoryOwnedByOrganizationAndProject<Token>
{
    Task<FindResults<Token>> GetByTypeAndUserIdAsync(TokenType type, string userId, CommandOptionsDescriptor<Token>? options = null);
    Task<FindResults<Token>> GetByTypeAndOrganizationIdAsync(TokenType type, string organizationId, CommandOptionsDescriptor<Token>? options = null);
    Task<FindResults<Token>> GetByTypeAndProjectIdAsync(TokenType type, string projectId, CommandOptionsDescriptor<Token>? options = null);
    Task<FindResults<Token>> GetByTypeAndProjectIdAndScopeAsync(TokenType type, string projectId, string scope, CommandOptionsDescriptor<Token>? options = null);
    Task<FindResults<Token>> GetByRefreshTokenAsync(string refreshToken, CommandOptionsDescriptor<Token>? options = null);
    Task<long> RemoveAllByUserIdAsync(string userId, CommandOptionsDescriptor<Token>? options = null);
}
