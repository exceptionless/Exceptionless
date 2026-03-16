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
            OrganizationId = "537650f3b77efe23a47914f3",
            ProjectId = "537650f3b77efe23a47914f4",
            Notes = "API access token"
        };

        // Act
        var result = _mapper.MapToToken(source);

        // Assert
        Assert.Equal("537650f3b77efe23a47914f3", result.OrganizationId);
        Assert.Equal("537650f3b77efe23a47914f4", result.ProjectId);
        Assert.Equal("API access token", result.Notes);
    }

    [Fact]
    public void MapToToken_WithNewToken_DoesNotSetTokenType()
    {
        // Arrange
        var source = new NewToken
        {
            OrganizationId = "537650f3b77efe23a47914f3"
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
            Id = "88cd0826e447a44e78877ab1",
            OrganizationId = "537650f3b77efe23a47914f3",
            ProjectId = "537650f3b77efe23a47914f4",
            UserId = "1ecd0826e447ad1e78822555",
            Notes = "Access token notes",
            Type = TokenType.Access
        };

        // Act
        var result = _mapper.MapToViewToken(source);

        // Assert
        Assert.Equal("88cd0826e447a44e78877ab1", result.Id);
        Assert.Equal("537650f3b77efe23a47914f3", result.OrganizationId);
        Assert.Equal("537650f3b77efe23a47914f4", result.ProjectId);
        Assert.Equal("1ecd0826e447ad1e78822555", result.UserId);
        Assert.Equal("Access token notes", result.Notes);
    }

    [Fact]
    public void MapToViewTokens_WithMultipleTokens_MapsAll()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new() { Id = "88cd0826e447a44e78877ab1", OrganizationId = "537650f3b77efe23a47914f3" },
            new() { Id = "88cd0826e447a44e78877ab2", OrganizationId = "1ecd0826e447ad1e78877666" },
            new() { Id = "88cd0826e447a44e78877ab3", OrganizationId = "1ecd0826e447ad1e78877777" }
        };

        // Act
        var result = _mapper.MapToViewTokens(tokens);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("88cd0826e447a44e78877ab1", result[0].Id);
        Assert.Equal("88cd0826e447a44e78877ab2", result[1].Id);
        Assert.Equal("88cd0826e447a44e78877ab3", result[2].Id);
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
