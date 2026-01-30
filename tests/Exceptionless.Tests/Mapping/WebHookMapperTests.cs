using Exceptionless.Core.Models;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Xunit;

namespace Exceptionless.Tests.Mapping;

public sealed class WebHookMapperTests
{
    private readonly WebHookMapper _mapper;

    public WebHookMapperTests()
    {
        _mapper = new WebHookMapper();
    }

    [Fact]
    public void MapToWebHook_WithValidNewWebHook_MapsAllProperties()
    {
        // Arrange
        var source = new NewWebHook
        {
            OrganizationId = "org123",
            ProjectId = "proj123",
            Url = "https://example.com/webhook",
            EventTypes = ["error", "log"]
        };

        // Act
        var result = _mapper.MapToWebHook(source);

        // Assert
        Assert.Equal("org123", result.OrganizationId);
        Assert.Equal("proj123", result.ProjectId);
        Assert.Equal("https://example.com/webhook", result.Url);
        Assert.Contains("error", result.EventTypes);
        Assert.Contains("log", result.EventTypes);
    }

    [Fact]
    public void MapToWebHook_WithNullProjectId_MapsWithNullProjectId()
    {
        // Arrange
        var source = new NewWebHook
        {
            OrganizationId = "org123",
            Url = "https://example.com/webhook"
        };

        // Act
        var result = _mapper.MapToWebHook(source);

        // Assert
        Assert.Equal("org123", result.OrganizationId);
        Assert.Null(result.ProjectId);
        Assert.Equal("https://example.com/webhook", result.Url);
    }

    [Fact]
    public void MapToWebHook_WithEmptyEventTypes_MapsEmptyEventTypes()
    {
        // Arrange
        var source = new NewWebHook
        {
            OrganizationId = "org123",
            Url = "https://example.com/webhook",
            EventTypes = []
        };

        // Act
        var result = _mapper.MapToWebHook(source);

        // Assert
        Assert.Empty(result.EventTypes);
    }
}
