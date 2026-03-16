using Exceptionless.Core.Models;
using Exceptionless.Web.Mapping;
using Xunit;

namespace Exceptionless.Tests.Mapping;

public sealed class UserMapperTests
{
    private readonly UserMapper _mapper;

    public UserMapperTests()
    {
        _mapper = new UserMapper();
    }

    [Fact]
    public void MapToViewUser_WithValidUser_MapsAllProperties()
    {
        // Arrange
        var source = new User
        {
            Id = "1ecd0826e447ad1e78822555",
            EmailAddress = "user@localhost",
            FullName = "Eric Smith",
            IsEmailAddressVerified = true,
            IsActive = true
        };

        // Act
        var result = _mapper.MapToViewUser(source);

        // Assert
        Assert.Equal("1ecd0826e447ad1e78822555", result.Id);
        Assert.Equal("user@localhost", result.EmailAddress);
        Assert.Equal("Eric Smith", result.FullName);
        Assert.True(result.IsEmailAddressVerified);
        Assert.True(result.IsActive);
    }

    [Fact]
    public void MapToViewUser_WithRoles_MapsRoles()
    {
        // Arrange
        var source = new User
        {
            Id = "1ecd0826e447ad1e78822555",
            EmailAddress = "admin@localhost",
            Roles = new HashSet<string> { "user", "admin" }
        };

        // Act
        var result = _mapper.MapToViewUser(source);

        // Assert
        Assert.Contains("user", result.Roles);
        Assert.Contains("admin", result.Roles);
    }

    [Fact]
    public void MapToViewUser_WithOrganizationIds_MapsOrganizationIds()
    {
        // Arrange
        var source = new User
        {
            Id = "1ecd0826e447ad1e78822555",
            EmailAddress = "user@localhost",
            OrganizationIds = new HashSet<string> { "537650f3b77efe23a47914f3", "1ecd0826e447ad1e78877666", "1ecd0826e447ad1e78877777" }
        };

        // Act
        var result = _mapper.MapToViewUser(source);

        // Assert
        Assert.Equal(3, result.OrganizationIds.Count);
        Assert.Contains("537650f3b77efe23a47914f3", result.OrganizationIds);
        Assert.Contains("1ecd0826e447ad1e78877666", result.OrganizationIds);
        Assert.Contains("1ecd0826e447ad1e78877777", result.OrganizationIds);
    }

    [Fact]
    public void MapToViewUsers_WithMultipleUsers_MapsAll()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Id = "1ecd0826e447ad1e78822555", EmailAddress = "user1@localhost" },
            new() { Id = "1ecd0826e447ad1e78822666", EmailAddress = "user2@localhost" }
        };

        // Act
        var result = _mapper.MapToViewUsers(users);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("1ecd0826e447ad1e78822555", result[0].Id);
        Assert.Equal("1ecd0826e447ad1e78822666", result[1].Id);
    }

    [Fact]
    public void MapToViewUsers_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var users = new List<User>();

        // Act
        var result = _mapper.MapToViewUsers(users);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void MapToViewUser_MutatingRoles_DoesNotAffectSource()
    {
        // Arrange
        var source = new User
        {
            Id = "1ecd0826e447ad1e78822555",
            EmailAddress = "admin@localhost",
            Roles = new HashSet<string> { "user", "admin", "global" }
        };

        // Act
        var result = _mapper.MapToViewUser(source);
        result.Roles.Remove("global");

        // Assert — source User.Roles is unaffected
        Assert.Contains("global", source.Roles);
        Assert.DoesNotContain("global", result.Roles);
    }
}
