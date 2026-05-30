using Exceptionless.Core.Services;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class NotificationServiceTests : TestWithServices
{
    private const string PrimaryOrganizationId = "664ec4c1f12e4f2b7a0d4001";

    public NotificationServiceTests(ITestOutputHelper output) : base(output) { }

    private NotificationService NotificationService => GetService<NotificationService>();

    [Fact]
    public async Task SetOrganizationNotificationSentAsync_WhenCalledInLastMillisecondOfUtcMonth_ShouldWriteObservableMarker()
    {
        // Arrange
        TimeProvider.SetUtcNow(new DateTime(2026, 5, 31, 23, 59, 59, 999, DateTimeKind.Utc));

        // Act
        await NotificationService.SetOrganizationNotificationSentAsync(PrimaryOrganizationId, isOverMonthlyLimit: true);

        // Assert
        Assert.True(await NotificationService.IsOrganizationNotificationSentAsync(PrimaryOrganizationId, isOverMonthlyLimit: true));
    }

    [Fact]
    public async Task TryAcquireOrganizationNotificationLockAsync_WhenOldHourlyBucketBoundaryPasses_ShouldRemainLocked()
    {
        // Arrange
        await using var firstLock = await NotificationService.TryAcquireOrganizationNotificationLockAsync(PrimaryOrganizationId, isOverMonthlyLimit: true);
        Assert.NotNull(firstLock);

        // Act
        TimeProvider.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(1)));

        // Assert
        Assert.True(await NotificationService.IsOrganizationNotificationLockedAsync(PrimaryOrganizationId, isOverMonthlyLimit: true));
        await using var secondLock = await NotificationService.TryAcquireOrganizationNotificationLockAsync(PrimaryOrganizationId, isOverMonthlyLimit: true);
        Assert.Null(secondLock);
    }
}
