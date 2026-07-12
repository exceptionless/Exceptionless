using Exceptionless.Core.Models.WorkItems;
using Xunit;

namespace Exceptionless.Tests.Jobs.WorkItemHandlers;

public class ForcePredefinedSavedViewsWorkItemTests
{
    [Fact]
    public void UniqueIdentifier_WhenForceUpdatesUseDifferentRunIds_IsDifferent()
    {
        // Arrange
        var first = new ForcePredefinedSavedViewsWorkItem
        {
            UserId = "user-id",
            RunId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        };
        var second = new ForcePredefinedSavedViewsWorkItem
        {
            UserId = "user-id",
            RunId = Guid.Parse("22222222-2222-2222-2222-222222222222")
        };

        // Act & Assert
        Assert.NotEqual(first.UniqueIdentifier, second.UniqueIdentifier);
    }
}
