using Exceptionless.Core.Models;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Xunit;

namespace Exceptionless.Tests.Mapping;

public sealed class OrganizationMapperTests
{
    private readonly OrganizationMapper _mapper;

    public OrganizationMapperTests()
    {
        _mapper = new OrganizationMapper(TimeProvider.System);
    }

    [Fact]
    public void MapToOrganization_WithValidNewOrganization_MapsName()
    {
        // Arrange
        var source = new NewOrganization { Name = "Test Organization" };

        // Act
        var result = _mapper.MapToOrganization(source);

        // Assert
        Assert.Equal("Test Organization", result.Name);
    }

    [Fact]
    public void MapToViewOrganization_WithValidOrganization_MapsAllProperties()
    {
        // Arrange
        var source = new Organization
        {
            Id = "org123",
            Name = "Test Organization",
            PlanId = "free",
            IsSuspended = false
        };

        // Act
        var result = _mapper.MapToViewOrganization(source);

        // Assert
        Assert.Equal("org123", result.Id);
        Assert.Equal("Test Organization", result.Name);
        Assert.Equal("free", result.PlanId);
        Assert.False(result.IsSuspended);
    }

    [Fact]
    public void MapToViewOrganization_WithSuspendedOrganization_MapsIsSuspended()
    {
        // Arrange
        var source = new Organization
        {
            Id = "org123",
            Name = "Suspended Org",
            IsSuspended = true
        };

        // Act
        var result = _mapper.MapToViewOrganization(source);

        // Assert
        Assert.True(result.IsSuspended);
    }

    [Fact]
    public void MapToViewOrganizations_WithMultipleOrganizations_MapsAll()
    {
        // Arrange
        var organizations = new List<Organization>
        {
            new() { Id = "org1", Name = "Organization 1" },
            new() { Id = "org2", Name = "Organization 2" },
            new() { Id = "org3", Name = "Organization 3" }
        };

        // Act
        var result = _mapper.MapToViewOrganizations(organizations);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("org1", result[0].Id);
        Assert.Equal("org2", result[1].Id);
        Assert.Equal("org3", result[2].Id);
    }

    [Fact]
    public void MapToViewOrganizations_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var organizations = new List<Organization>();

        // Act
        var result = _mapper.MapToViewOrganizations(organizations);

        // Assert
        Assert.Empty(result);
    }
}
