using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Models;
using Xunit;

namespace Exceptionless.Tests.Jobs.WorkItemHandlers;

public class ForcePredefinedSavedViewsWorkItemHandlerTests
{
    [Fact]
    public void GetUniqueSlug_QueuedViewWithoutId_ReturnsUniqueSlug()
    {
        // Arrange
        var existingViews = new[]
        {
            new SavedView
            {
                Id = "existing-view",
                Slug = "foo"
            },
            new SavedView
            {
                Id = null!,
                Slug = "foo-2"
            }
        };

        // Act
        string slug = ForcePredefinedSavedViewsWorkItemHandler.GetUniqueSlug("foo", existingViews, null);

        // Assert
        Assert.Equal("foo-3", slug);
    }
}
