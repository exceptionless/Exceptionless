using Exceptionless.Core.Models;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Xunit;

namespace Exceptionless.Tests.Mapping;

public sealed class SavedViewMapperTests
{
    private readonly SavedViewMapper _mapper;

    public SavedViewMapperTests()
    {
        _mapper = new SavedViewMapper();
    }

    [Fact]
    public void MapToSavedView_WithValidNewSavedView_MapsAllProperties()
    {
        // Arrange
        var source = new NewSavedView
        {
            OrganizationId = "537650f3b77efe23a47914f3",
            Name = "Open Issues",
            Filter = "(status:open OR status:regressed)",
            Time = "[now-7d TO now]",
            Sort = "-last",
            ViewType = "stacks",
            FilterDefinitions = "[{\"type\":\"status\",\"values\":[\"open\",\"regressed\"]}]",
            Columns = new Dictionary<string, bool> { ["status"] = true, ["users"] = false },
            ColumnOrder = ["summary", "status", "users"]
        };

        // Act
        var result = _mapper.MapToSavedView(source);

        // Assert
        Assert.Equal("537650f3b77efe23a47914f3", result.OrganizationId);
        Assert.Equal("Open Issues", result.Name);
        Assert.Equal("(status:open OR status:regressed)", result.Filter);
        Assert.Equal("[now-7d TO now]", result.Time);
        Assert.Equal("-last", result.Sort);
        Assert.Equal("stacks", result.ViewType);
        Assert.Equal("[{\"type\":\"status\",\"values\":[\"open\",\"regressed\"]}]", result.FilterDefinitions);
        Assert.NotNull(result.Columns);
        Assert.True(result.Columns["status"]);
        Assert.False(result.Columns["users"]);
        Assert.Equal(["summary", "status", "users"], result.ColumnOrder);
    }

    [Fact]
    public void MapToSavedView_IgnoredFields_AreNotMapped()
    {
        // Arrange
        var source = new NewSavedView
        {
            OrganizationId = "537650f3b77efe23a47914f3",
            Name = "Test View",
            ViewType = "events"
        };

        // Act
        var result = _mapper.MapToSavedView(source);

        // Assert - Version, CreatedByUserId, UpdatedByUserId are ignored by mapper
        // UserId is mapped when set on NewSavedView (it's JsonIgnore for request deserialization only)
        Assert.Equal(1, result.Version);
        Assert.Null(result.CreatedByUserId);
        Assert.Null(result.UpdatedByUserId);
        Assert.Null(result.UserId);
    }

    [Fact]
    public void MapToSavedView_WithNullOptionalFields_MapsNulls()
    {
        // Arrange
        var source = new NewSavedView
        {
            OrganizationId = "537650f3b77efe23a47914f3",
            Name = "Minimal View",
            ViewType = "stream"
        };

        // Act
        var result = _mapper.MapToSavedView(source);

        // Assert
        Assert.Null(result.Filter);
        Assert.Null(result.Time);
        Assert.Null(result.FilterDefinitions);
        Assert.Null(result.Columns);
        Assert.Null(result.ColumnOrder);
    }

    [Fact]
    public void MapToViewSavedView_WithValidSavedView_MapsAllProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var source = new SavedView
        {
            Id = "88cd0826e447a44e78877ab1",
            OrganizationId = "537650f3b77efe23a47914f3",
            UserId = "1ecd0826e447ad1e78822555",
            CreatedByUserId = "1ecd0826e447ad1e78822555",
            UpdatedByUserId = "1ecd0826e447ad1e78822666",
            Filter = "status:open",
            FilterDefinitions = "[{\"type\":\"status\",\"values\":[\"open\"]}]",
            Columns = new Dictionary<string, bool> { ["status"] = true },
            ColumnOrder = ["summary", "status"],
            Name = "My View",
            Time = "[now-30d TO now]",
            Sort = "-last",
            Version = 1,
            ViewType = "stacks",
            CreatedUtc = now.AddDays(-1),
            UpdatedUtc = now
        };

        // Act
        var result = _mapper.MapToViewSavedView(source);

        // Assert
        Assert.Equal("88cd0826e447a44e78877ab1", result.Id);
        Assert.Equal("537650f3b77efe23a47914f3", result.OrganizationId);
        Assert.Equal("1ecd0826e447ad1e78822555", result.UserId);
        Assert.Equal("1ecd0826e447ad1e78822555", result.CreatedByUserId);
        Assert.Equal("1ecd0826e447ad1e78822666", result.UpdatedByUserId);
        Assert.Equal("status:open", result.Filter);
        Assert.Equal("[{\"type\":\"status\",\"values\":[\"open\"]}]", result.FilterDefinitions);
        Assert.NotNull(result.Columns);
        Assert.True(result.Columns["status"]);
        Assert.Equal(["summary", "status"], result.ColumnOrder);
        Assert.Equal("My View", result.Name);
        Assert.Equal("[now-30d TO now]", result.Time);
        Assert.Equal("-last", result.Sort);
        Assert.Equal(1, result.Version);
        Assert.Equal("stacks", result.ViewType);
        Assert.Equal(now.AddDays(-1), result.CreatedUtc);
        Assert.Equal(now, result.UpdatedUtc);
    }

    [Fact]
    public void MapToViewSavedView_WithNullOptionalFields_MapsNulls()
    {
        // Arrange
        var source = new SavedView
        {
            Id = "88cd0826e447a44e78877ab1",
            OrganizationId = "537650f3b77efe23a47914f3",
            CreatedByUserId = "1ecd0826e447ad1e78822555",
            Name = "Organization Wide View",
            ViewType = "events",
            Version = 1
        };

        // Act
        var result = _mapper.MapToViewSavedView(source);

        // Assert
        Assert.Null(result.UserId);
        Assert.Null(result.UpdatedByUserId);
        Assert.Null(result.Filter);
        Assert.Null(result.FilterDefinitions);
        Assert.Null(result.Columns);
        Assert.Null(result.ColumnOrder);
        Assert.Null(result.Time);
        Assert.Null(result.Sort);
    }

    [Fact]
    public void MapToViewSavedViews_WithMultipleSavedViews_MapsAll()
    {
        // Arrange
        var views = new List<SavedView>
        {
            new() { Id = "88cd0826e447a44e78877ab1", OrganizationId = "537650f3b77efe23a47914f3", CreatedByUserId = "1ecd0826e447ad1e78822555", Name = "View 1", ViewType = "events" },
            new() { Id = "88cd0826e447a44e78877ab2", OrganizationId = "537650f3b77efe23a47914f3", CreatedByUserId = "1ecd0826e447ad1e78822555", Name = "View 2", ViewType = "stacks" },
            new() { Id = "88cd0826e447a44e78877ab3", OrganizationId = "537650f3b77efe23a47914f3", CreatedByUserId = "1ecd0826e447ad1e78822555", Name = "View 3", ViewType = "stream" }
        };

        // Act
        var result = _mapper.MapToViewSavedViews(views);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("88cd0826e447a44e78877ab1", result[0].Id);
        Assert.Equal("88cd0826e447a44e78877ab2", result[1].Id);
        Assert.Equal("88cd0826e447a44e78877ab3", result[2].Id);
    }

    [Fact]
    public void MapToViewSavedViews_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var views = new List<SavedView>();

        // Act
        var result = _mapper.MapToViewSavedViews(views);

        // Assert
        Assert.Empty(result);
    }
}
