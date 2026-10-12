using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Utility;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Jobs.WorkItemHandlers;

public sealed class RemoveBotEventsWorkItemHandlerTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Fact]
    public async Task HandleItemAsync_CleanupTask_RestrictsDeletionToOwningProject()
    {
        // Arrange
        var item = CreateWorkItem();
        var repository = new Mock<IEventRepository>(MockBehavior.Strict);
        repository.Setup(r => r.RemoveAllByProjectAndClientIpAsync(item.OrganizationId, item.ProjectId,
                item.ClientIpAddress, item.UtcStartDate, item.UtcEndDate, null))
            .ReturnsAsync(1L);
        var handler = new RemoveBotEventsWorkItemHandler(repository.Object, GetService<ILockProvider>(), Log);
        var context = CreateContext(item);

        // Act
        await handler.HandleItemAsync(context);

        // Assert
        repository.Verify(r => r.RemoveAllByProjectAndClientIpAsync(item.OrganizationId, item.ProjectId,
            item.ClientIpAddress, item.UtcStartDate, item.UtcEndDate, null), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task HandleItemAsync_LegacyTaskWithoutProject_RejectsUnscopedDeletion(string? projectId)
    {
        // Arrange
        var repository = new Mock<IEventRepository>(MockBehavior.Strict);
        var handler = new RemoveBotEventsWorkItemHandler(repository.Object, GetService<ILockProvider>(), Log);
        var item = CreateWorkItem() with { ProjectId = projectId! };
        var context = CreateContext(item);

        // Act
        var exception = await Record.ExceptionAsync(() => handler.HandleItemAsync(context));

        // Assert
        Assert.IsAssignableFrom<ArgumentException>(exception);
        repository.VerifyNoOtherCalls();
    }

    private static RemoveBotEventsWorkItem CreateWorkItem() => new()
    {
        OrganizationId = TestConstants.OrganizationId,
        ProjectId = TestConstants.ProjectId,
        ClientIpAddress = "203.0.113.10",
        UtcStartDate = new DateTime(2026, 9, 21, 2, 25, 0, DateTimeKind.Utc),
        UtcEndDate = new DateTime(2026, 9, 21, 2, 30, 0, DateTimeKind.Utc)
    };

    private WorkItemContext CreateContext(RemoveBotEventsWorkItem item) =>
        new(item, "test-job", EmptyLock.Empty, TestCancellationToken, static (_, _) => Task.CompletedTask);
}
