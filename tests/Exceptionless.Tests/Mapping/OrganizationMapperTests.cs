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
            Id = "537650f3b77efe23a47914f3",
            Name = "Acme Organization",
            PlanId = "free",
            IsSuspended = false
        };

        // Act
        var result = _mapper.MapToViewOrganization(source);

        // Assert
        Assert.Equal("537650f3b77efe23a47914f3", result.Id);
        Assert.Equal("Acme Organization", result.Name);
        Assert.Equal("free", result.PlanId);
        Assert.False(result.IsSuspended);
    }

    [Fact]
    public void MapToViewOrganization_WithSuspendedOrganization_MapsIsSuspended()
    {
        // Arrange
        var source = new Organization
        {
            Id = "537650f3b77efe23a47914f3",
            Name = "Suspended Organization",
            IsSuspended = true
        };

        // Act
        var result = _mapper.MapToViewOrganization(source);

        // Assert
        Assert.True(result.IsSuspended);
    }

    [Fact]
    public void MapToViewOrganization_WithSuspensionCode_MapsEnumToString()
    {
        // Arrange
        var source = new Organization
        {
            Id = "537650f3b77efe23a47914f3",
            Name = "Suspended Organization",
            IsSuspended = true,
            SuspensionCode = SuspensionCode.Billing
        };

        // Act
        var result = _mapper.MapToViewOrganization(source);

        // Assert
        Assert.Equal("Billing", result.SuspensionCode);
    }

    [Fact]
    public void MapToViewOrganization_WithNullSuspensionCode_MapsToNull()
    {
        // Arrange
        var source = new Organization
        {
            Id = "537650f3b77efe23a47914f3",
            Name = "Active Organization",
            SuspensionCode = null
        };

        // Act
        var result = _mapper.MapToViewOrganization(source);

        // Assert
        Assert.Null(result.SuspensionCode);
    }

    [Fact]
    public void MapToViewOrganizations_WithMultipleOrganizations_MapsAll()
    {
        // Arrange
        var organizations = new List<Organization>
        {
            new() { Id = "537650f3b77efe23a47914f3", Name = "Organization 1" },
            new() { Id = "1ecd0826e447ad1e78877666", Name = "Organization 2" },
            new() { Id = "1ecd0826e447ad1e78877777", Name = "Organization 3" }
        };

        // Act
        var result = _mapper.MapToViewOrganizations(organizations);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("537650f3b77efe23a47914f3", result[0].Id);
        Assert.Equal("1ecd0826e447ad1e78877666", result[1].Id);
        Assert.Equal("1ecd0826e447ad1e78877777", result[2].Id);
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
