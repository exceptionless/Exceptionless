using System.Globalization;
using System.Security.Claims;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services.BackgroundTasks;
using Exceptionless.Core.Services.BulkOperations;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Api.Handlers;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Extensions;
using Foundatio.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Api.Handlers;

public class BackgroundTaskHandlerTests : TestWithLoggingBase
{
    private static readonly DateTimeOffset UtcNow = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    public BackgroundTaskHandlerTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(false, BackgroundTaskStatus.Cancelled, 1)]
    [InlineData(true, BackgroundTaskStatus.Running, 0)]
    public async Task Handle_CancelBulkTask_ReleasesSelectionOnlyAfterItBecomesTerminal(bool running, BackgroundTaskStatus expectedStatus, int expectedReleases)
    {
        // Arrange
        const string organizationId = "537650f3b77efe23a47914f4";
        const string pinnedSelection = "saved-event-selection";
        var time = new FakeTimeProvider();
        using var harness = new BackgroundTaskTestHarness(time, Log);
        var user = new User
        {
            Id = "537650f3b77efe23a47914f8",
            EmailAddress = "user@example.test",
            OrganizationIds = new HashSet<string> { organizationId }
        };
        var task = new BackgroundTask
        {
            OrganizationId = organizationId,
            Type = DeleteEventsBulkOperationHandler.OperationType,
            Description = "Delete matching events",
            CreatedByUserId = user.Id,
            CanCancel = true,
            Bulk = new BulkOperationDetails { Selection = new BulkOperationSelection { Kind = BulkOperationSelectionKind.Filter } }
        };
        await harness.Tasks.AddAsync(task);
        await harness.Cache.SetAsync(BulkOperationService.GetPinnedSelectionCacheKey(task.Id), pinnedSelection, BackgroundTaskService.TaskLifetime);
        if (running)
        {
            await harness.Tasks.UpdateAsync(task.Id, current =>
            {
                current.Start(time.GetUtcNow().UtcDateTime);
                return true;
            });
        }

        var operation = new Mock<IBulkOperationHandler>(MockBehavior.Strict);
        operation.SetupGet(handler => handler.Type).Returns(task.Type);
        operation.SetupGet(handler => handler.Entity).Returns(BulkOperationEntity.Events);
        var resolver = new Mock<IBulkSelectionResolver>(MockBehavior.Strict);
        resolver.SetupGet(selection => selection.Entity).Returns(BulkOperationEntity.Events);
        resolver.Setup(selection => selection.ReleaseAsync(It.IsAny<BackgroundTask>(), pinnedSelection, null)).Returns(Task.CompletedTask);
        var bulkService = new BulkOperationService(harness.Tasks, harness.Cache, harness.LockProvider, [operation.Object], [resolver.Object], time);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(user.ToIdentity()) };
        context.Request.SetUser(user);
        var organizations = new Mock<IOrganizationRepository>(MockBehavior.Strict);
        var projects = new Mock<IProjectRepository>(MockBehavior.Strict);
        var handler = new BackgroundTaskHandler(harness.Tasks, bulkService, [resolver.Object], organizations.Object, projects.Object,
            new AppOptions(), time, new HttpContextAccessor { HttpContext = context }, Log.CreateLogger<BackgroundTaskHandler>());

        // Act
        var result = await handler.Handle(new CancelBackgroundTask(task.Id));
        var stored = await harness.Tasks.GetAsync(task.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedStatus, result.Value.Value.Status);
        Assert.Equal(expectedStatus, stored!.Status);
        Assert.True(await harness.Tasks.IsCancelRequestedAsync(task.Id));
        resolver.Verify(selection => selection.ReleaseAsync(It.IsAny<BackgroundTask>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Exactly(expectedReleases));
        resolver.Verify(selection => selection.ReleaseAsync(
            It.Is<BackgroundTask>(current => current.Id == task.Id && current.Status == BackgroundTaskStatus.Cancelled), pinnedSelection, null), Times.Exactly(expectedReleases));
    }

    [Theory]
    [InlineData("not a date")]
    [InlineData("24h")]
    [InlineData("date|not a date")]
    [InlineData("date|")]
    [InlineData("created_utc|[now-1d TO now]")]
    [InlineData("-")]
    [InlineData("[ - ]")]
    [InlineData("- now")]
    [InlineData("now -")]
    [InlineData("[now-1d TO now")]
    [InlineData("[now/d TO now-1d/d]")]
    [InlineData("last 0 days")]
    [InlineData("last 999999999999999999 days")]
    public void TryParseSelectionTime_InvalidRange_IsRejectedBeforeDefaultsCanBroadenIt(string time)
    {
        // Arrange
        var relativeBaseTime = UtcNow;

        // Act
        bool parsed = BackgroundTaskHandler.TryParseSelectionTime(time, relativeBaseTime, out var range);

        // Assert
        Assert.False(parsed);
        Assert.Null(range);
    }

    [Theory]
    [InlineData(null, 0, "0001-01-01T00:00:00Z", "9999-12-31T23:59:59.9999999Z")]
    [InlineData("", 0, "0001-01-01T00:00:00Z", "9999-12-31T23:59:59.9999999Z")]
    [InlineData("[now-1d TO now]", 0, "2026-11-02T12:00:00Z", "2026-11-03T12:00:00Z")]
    [InlineData("date|[now/d TO now/d]", -5, "2026-11-03T05:00:00Z", "2026-11-04T04:59:59.999Z")]
    [InlineData("[now-1d TO *]", 0, "2026-11-02T12:00:00Z", "9999-12-31T23:59:59.9999999Z")]
    [InlineData("[* TO now]", 0, "0001-01-01T00:00:00Z", "2026-11-03T12:00:00Z")]
    [InlineData("[* TO *]", 0, "0001-01-01T00:00:00Z", "9999-12-31T23:59:59.9999999Z")]
    [InlineData("{* TO *}", 0, "0001-01-01T00:00:00Z", "9999-12-31T23:59:59.9999999Z")]
    [InlineData("<now/d", 0, "0001-01-01T00:00:00Z", "2026-11-03T00:00:00Z")]
    [InlineData("last 7 days", 0, "2026-10-27T00:00:00Z", "2026-11-03T12:00:00Z")]
    [InlineData("date|[2026-11-01T00:00:00Z||+1d/d TO 2026-11-01T00:00:00Z||+2d/d]", 0, "2026-11-02T00:00:00Z", "2026-11-03T23:59:59.999Z")]
    [InlineData("[2026-11-01T00:00:00.123Z TO 2026-11-01T00:00:00.123Z]", 0, "2026-11-01T00:00:00.123Z", "2026-11-01T00:00:00.123Z")]
    public void TryParseSelectionTime_ValidRange_PreservesTheRequestedBounds(string? time, int offsetHours, string expectedStart, string expectedEnd)
    {
        // Arrange
        var relativeBaseTime = UtcNow.ToOffset(TimeSpan.FromHours(offsetHours));

        // Act
        bool parsed = BackgroundTaskHandler.TryParseSelectionTime(time, relativeBaseTime, out var range);

        // Assert
        Assert.True(parsed);
        Assert.NotNull(range);
        Assert.Equal(DateTimeOffset.Parse(expectedStart, CultureInfo.InvariantCulture).UtcDateTime, range.UtcStart);
        Assert.Equal(DateTimeOffset.Parse(expectedEnd, CultureInfo.InvariantCulture).UtcDateTime, range.UtcEnd);
    }

    [Theory]
    [InlineData(null, true, 0)]
    [InlineData("", true, 0)]
    [InlineData("-5h", true, -300)]
    [InlineData("330m", true, 330)]
    [InlineData("14h", true, 840)]
    [InlineData("-14h", true, -840)]
    [InlineData("not an offset", false, 0)]
    [InlineData("15h", false, 0)]
    [InlineData("1s", false, 0)]
    [InlineData("2147483647d", false, 0)]
    public void TryParseSelectionOffset_ValidatesOffsetsWithoutThrowingOrFallingBackToUtc(string? offset, bool expectedSuccess, int expectedMinutes)
    {
        // Arrange
        var expectedOffset = TimeSpan.FromMinutes(expectedMinutes);

        // Act
        bool parsed = BackgroundTaskHandler.TryParseSelectionOffset(offset, out var utcOffset);

        // Assert
        Assert.Equal(expectedSuccess, parsed);
        Assert.Equal(expectedOffset, utcOffset);
    }
}
