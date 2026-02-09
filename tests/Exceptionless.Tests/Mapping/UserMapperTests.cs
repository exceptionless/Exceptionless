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
            Id = "user123",
            EmailAddress = "test@example.com",
            FullName = "Test User",
            IsEmailAddressVerified = true,
            IsActive = true
        };

        // Act
        var result = _mapper.MapToViewUser(source);

        // Assert
        Assert.Equal("user123", result.Id);
        Assert.Equal("test@example.com", result.EmailAddress);
        Assert.Equal("Test User", result.FullName);
        Assert.True(result.IsEmailAddressVerified);
        Assert.True(result.IsActive);
    }

    [Fact]
    public void MapToViewUser_WithRoles_MapsRoles()
    {
        // Arrange
        var source = new User
        {
            Id = "user123",
            EmailAddress = "admin@example.com",
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
            Id = "user123",
            EmailAddress = "user@example.com",
            OrganizationIds = new HashSet<string> { "org1", "org2", "org3" }
        };

        // Act
        var result = _mapper.MapToViewUser(source);

        // Assert
        Assert.Equal(3, result.OrganizationIds.Count);
        Assert.Contains("org1", result.OrganizationIds);
        Assert.Contains("org2", result.OrganizationIds);
        Assert.Contains("org3", result.OrganizationIds);
    }

    [Fact]
    public void MapToViewUsers_WithMultipleUsers_MapsAll()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Id = "user1", EmailAddress = "user1@example.com" },
            new() { Id = "user2", EmailAddress = "user2@example.com" }
        };

        // Act
        var result = _mapper.MapToViewUsers(users);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("user1", result[0].Id);
        Assert.Equal("user2", result[1].Id);
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
}
