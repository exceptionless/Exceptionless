using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Exceptionless.Tests.Pipeline;

public class CheckEventDateActionTests
{
    [Fact]
    public async Task ProcessAsync_WithEventOlderThanThreeDays_DiscardsEvent()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        var context = CreateContext(utcNow.AddDays(-4), retentionDays: 7);
        var action = CreateAction(utcNow);

        // Act
        await action.ProcessAsync(context);

        // Assert
        Assert.True(context.IsCancelled);
        Assert.True(context.IsDiscarded);
    }

    [Fact]
    public async Task ProcessAsync_WithExtendedDateRangeAndEventWithinRetention_KeepsEvent()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        var context = CreateContext(utcNow.AddDays(-7), retentionDays: 7);
        context.AllowExtendedEventDateRange = true;
        var action = CreateAction(utcNow);

        // Act
        await action.ProcessAsync(context);

        // Assert
        Assert.False(context.IsCancelled);
        Assert.False(context.IsDiscarded);
    }

    [Fact]
    public async Task ProcessAsync_WithExtendedDateRangeAndEventOutsideRetention_DiscardsEvent()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        var context = CreateContext(utcNow.AddDays(-8), retentionDays: 7);
        context.AllowExtendedEventDateRange = true;
        var action = CreateAction(utcNow);

        // Act
        await action.ProcessAsync(context);

        // Assert
        Assert.True(context.IsCancelled);
        Assert.True(context.IsDiscarded);
    }

    private static CheckEventDateAction CreateAction(DateTimeOffset utcNow)
    {
        return new CheckEventDateAction(new FixedTimeProvider(utcNow), CreateOptions(), NullLoggerFactory.Instance);
    }

    private static EventContext CreateContext(DateTimeOffset eventDate, int retentionDays)
    {
        var ev = new PersistentEvent { Date = eventDate };
        var organization = new Organization { RetentionDays = retentionDays };
        var project = new Project();

        return new EventContext(ev, organization, project);
    }

    private static AppOptions CreateOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [nameof(AppOptions.BaseURL)] = "http://localhost"
            })
            .Build();

        return AppOptions.ReadFromConfiguration(configuration);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}