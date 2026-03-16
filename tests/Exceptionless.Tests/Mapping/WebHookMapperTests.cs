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
            OrganizationId = "537650f3b77efe23a47914f3",
            ProjectId = "537650f3b77efe23a47914f4",
            Url = "https://localhost/webhook",
            EventTypes = ["error", "log"]
        };

        // Act
        var result = _mapper.MapToWebHook(source);

        // Assert
        Assert.Equal("537650f3b77efe23a47914f3", result.OrganizationId);
        Assert.Equal("537650f3b77efe23a47914f4", result.ProjectId);
        Assert.Equal("https://localhost/webhook", result.Url);
        Assert.Contains("error", result.EventTypes);
        Assert.Contains("log", result.EventTypes);
    }

    [Fact]
    public void MapToWebHook_WithNullProjectId_MapsWithNullProjectId()
    {
        // Arrange
        var source = new NewWebHook
        {
            OrganizationId = "537650f3b77efe23a47914f3",
            Url = "https://localhost/webhook"
        };

        // Act
        var result = _mapper.MapToWebHook(source);

        // Assert
        Assert.Equal("537650f3b77efe23a47914f3", result.OrganizationId);
        Assert.Null(result.ProjectId);
        Assert.Equal("https://localhost/webhook", result.Url);
    }

    [Fact]
    public void MapToWebHook_WithEmptyEventTypes_MapsEmptyEventTypes()
    {
        // Arrange
        var source = new NewWebHook
        {
            OrganizationId = "537650f3b77efe23a47914f3",
            Url = "https://localhost/webhook",
            EventTypes = []
        };

        // Act
        var result = _mapper.MapToWebHook(source);

        // Assert
        Assert.Empty(result.EventTypes);
    }
}
