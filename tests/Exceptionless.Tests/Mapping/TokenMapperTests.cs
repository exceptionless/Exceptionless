using Exceptionless.Core.Models;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Xunit;

namespace Exceptionless.Tests.Mapping;

public sealed class TokenMapperTests
{
    private readonly TokenMapper _mapper;

    public TokenMapperTests()
    {
        _mapper = new TokenMapper();
    }

    [Fact]
    public void MapToToken_WithValidNewToken_MapsOrganizationIdAndProjectId()
    {
        // Arrange
        var source = new NewToken
        {
            OrganizationId = "org123",
            ProjectId = "proj123",
            Notes = "Test token"
        };

        // Act
        var result = _mapper.MapToToken(source);

        // Assert
        Assert.Equal("org123", result.OrganizationId);
        Assert.Equal("proj123", result.ProjectId);
        Assert.Equal("Test token", result.Notes);
    }

    [Fact]
    public void MapToToken_WithNewToken_DoesNotSetTokenType()
    {
        // Arrange
        var source = new NewToken
        {
            OrganizationId = "org123"
        };

        // Act
        var result = _mapper.MapToToken(source);

        // Assert - TokenType is ignored in mapping, so it defaults to Authentication
        Assert.Equal(TokenType.Authentication, result.Type);
    }

    [Fact]
    public void MapToViewToken_WithValidToken_MapsAllProperties()
    {
        // Arrange
        var source = new Token
        {
            Id = "token123",
            OrganizationId = "org123",
            ProjectId = "proj123",
            UserId = "user123",
            Notes = "Test notes",
            Type = TokenType.Access
        };

        // Act
        var result = _mapper.MapToViewToken(source);

        // Assert
        Assert.Equal("token123", result.Id);
        Assert.Equal("org123", result.OrganizationId);
        Assert.Equal("proj123", result.ProjectId);
        Assert.Equal("user123", result.UserId);
        Assert.Equal("Test notes", result.Notes);
    }

    [Fact]
    public void MapToViewTokens_WithMultipleTokens_MapsAll()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new() { Id = "token1", OrganizationId = "org1" },
            new() { Id = "token2", OrganizationId = "org2" },
            new() { Id = "token3", OrganizationId = "org3" }
        };

        // Act
        var result = _mapper.MapToViewTokens(tokens);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("token1", result[0].Id);
        Assert.Equal("token2", result[1].Id);
        Assert.Equal("token3", result[2].Id);
    }

    [Fact]
    public void MapToViewTokens_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var tokens = new List<Token>();

        // Act
        var result = _mapper.MapToViewTokens(tokens);

        // Assert
        Assert.Empty(result);
    }
}
