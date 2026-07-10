using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Xunit;

namespace Exceptionless.Tests.Pipeline;

public class UpdateRateCountersActionTests
{
    [Fact]
    public void GetCounterKeys_DuplicateRules_ReturnsOneCounterKey()
    {
        // Arrange
        var ev = new PersistentEvent
        {
            ProjectId = "project-1",
            StackId = "stack-1",
            Type = Event.KnownTypes.Error
        };
        var rules = new[]
        {
            CreateRule("rule-1", RateNotificationSubject.Project),
            CreateRule("rule-2", RateNotificationSubject.Project)
        };

        // Act
        var keys = UpdateRateCountersAction.GetCounterKeys(ev, false, false, rules);

        // Assert
        Assert.Equal(["project:project-1:signal:Errors"], keys);
    }

    [Fact]
    public void GetCounterKeys_ProjectAndMatchingStackRules_ReturnsDistinctScopeKeys()
    {
        // Arrange
        var ev = new PersistentEvent
        {
            ProjectId = "project-1",
            StackId = "stack-1",
            Type = Event.KnownTypes.Error
        };
        var rules = new[]
        {
            CreateRule("rule-1", RateNotificationSubject.Project),
            CreateRule("rule-2", RateNotificationSubject.Stack, "stack-1"),
            CreateRule("rule-3", RateNotificationSubject.Stack, "stack-2")
        };

        // Act
        var keys = UpdateRateCountersAction.GetCounterKeys(ev, false, false, rules);

        // Assert
        Assert.Equal(2, keys.Count);
        Assert.Contains("project:project-1:signal:Errors", keys);
        Assert.Contains("project:project-1:stack:stack-1:signal:Errors", keys);
    }

    private static RateNotificationRule CreateRule(string id, RateNotificationSubject subject, string? stackId = null)
    {
        return new RateNotificationRule
        {
            Id = id,
            OrganizationId = "organization-1",
            ProjectId = "project-1",
            UserId = "user-1",
            Name = id,
            IsEnabled = true,
            Signal = RateNotificationSignal.Errors,
            Subject = subject,
            StackId = stackId,
            Threshold = 1,
            Window = TimeSpan.FromMinutes(5),
            Cooldown = TimeSpan.FromMinutes(5)
        };
    }
}
