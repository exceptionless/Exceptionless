using System.Threading;
using Exceptionless.Core.Services;
using Foundatio.Lock;
using Foundatio.Messaging;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class NotificationServiceTests : TestWithServices
{
    private const string PrimaryOrganizationId = "664ec4c1f12e4f2b7a0d4001";

    public NotificationServiceTests(ITestOutputHelper output) : base(output) { }

    private NotificationService NotificationService => GetService<NotificationService>();

    [Fact]
    public async Task IsOrganizationNotificationLockedAsync_WhenNoLockIsHeld_ShouldNotAcquireAndReleaseLock()
    {
        // Arrange
        var messageBus = GetService<IMessageBus>();
        var releaseCount = 0;
        await messageBus.SubscribeAsync<CacheLockReleased>(_ =>
        {
            Interlocked.Increment(ref releaseCount);
            return Task.CompletedTask;
        }, TestCancellationToken);

        // Act
        var isLocked = await NotificationService.IsOrganizationNotificationLockedAsync(PrimaryOrganizationId, isOverMonthlyLimit: true);

        // Assert
        Assert.False(isLocked);
        Assert.Equal(0, Volatile.Read(ref releaseCount));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task OrganizationNotificationMethods_WhenOrganizationIdIsNullOrEmpty_ShouldThrowArgumentException(string? organizationId)
    {
        // Arrange
        const bool IsOverMonthlyLimit = true;

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => NotificationService.IsOrganizationNotificationSentAsync(organizationId!, IsOverMonthlyLimit));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => NotificationService.SetOrganizationNotificationSentAsync(organizationId!, IsOverMonthlyLimit));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => NotificationService.RemoveOrganizationNotificationSentAsync(organizationId!, IsOverMonthlyLimit));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => NotificationService.TryAcquireOrganizationNotificationLockAsync(organizationId!, IsOverMonthlyLimit));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => NotificationService.IsOrganizationNotificationLockedAsync(organizationId!, IsOverMonthlyLimit));
    }

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
