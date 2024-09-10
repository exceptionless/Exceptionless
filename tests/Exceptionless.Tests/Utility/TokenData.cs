using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Utility;

namespace Exceptionless.Tests.Utility;

public class TokenData
{
    private readonly TimeProvider _timeProvider;

    public TokenData(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public Token GenerateSampleApiKeyToken()
    {
        return GenerateToken(id: TestConstants.ApiKey, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId);
    }

    public Token GenerateSampleUserToken()
    {
        return GenerateToken(id: TestConstants.UserApiKey, userId: TestConstants.UserId, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId, type: TokenType.Authentication);
    }

    public Token GenerateToken(bool generateId = false, string? id = null, string? userId = null, string? organizationId = null, string? projectId = null, TokenType type = TokenType.Access, string? notes = null)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var token = new Token
        {
            Id = id.IsNullOrEmpty() ? generateId ? ObjectId.GenerateNewId().ToString() : TestConstants.ApiKey : id,
            UserId = userId,
            OrganizationId = organizationId!,
            ProjectId = projectId!,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow,
            CreatedBy = userId ?? TestConstants.UserId,
            Type = type,
            Notes = notes
        };

        return token;
    }
}
