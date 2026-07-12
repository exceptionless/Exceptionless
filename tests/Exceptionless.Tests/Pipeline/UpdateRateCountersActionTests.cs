using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Services;
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
        var plan = RateNotificationCounterPlan.Compile(ev.ProjectId, rules);
        var keys = UpdateRateCountersAction.GetCounterKeys(ev, false, false, plan);

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
        var plan = RateNotificationCounterPlan.Compile(ev.ProjectId, rules);
        var keys = UpdateRateCountersAction.GetCounterKeys(ev, false, false, plan);

        // Assert
        Assert.Equal(2, keys.Count);
        Assert.Contains("project:project-1:signal:Errors", keys);
        Assert.Contains("project:project-1:stack:stack-1:signal:Errors", keys);
    }

    [Fact]
    public void Compile_ManyRulesForSameScope_ProducesOneHotPathCounter()
    {
        // Arrange
        var rules = Enumerable.Range(0, 1000)
            .Select(index => CreateRule($"rule-{index}", RateNotificationSubject.Project));

        // Act
        var plan = RateNotificationCounterPlan.Compile("project-1", rules);

        // Assert
        Assert.Equal(1000, plan.RuleCount);
        Assert.Single(plan.ProjectCounters);
        Assert.Empty(plan.StackCounters);
    }

    [Fact]
    public void GetCounterKeys_AllSignalsAndScopes_HasFixedTenIncrementMaximum()
    {
        // Arrange
        var rules = Enum.GetValues<RateNotificationSignal>()
            .SelectMany(signal => Enumerable.Range(0, 100).SelectMany(index => new[]
            {
                CreateRule($"project-{signal}-{index}", RateNotificationSubject.Project, signal: signal),
                CreateRule($"stack-{signal}-{index}", RateNotificationSubject.Stack, "stack-1", signal)
            }));
        var plan = RateNotificationCounterPlan.Compile("project-1", rules);
        var ev = new PersistentEvent
        {
            ProjectId = "project-1",
            StackId = "stack-1",
            Type = Event.KnownTypes.Error,
            Tags = [Event.KnownTags.Critical]
        };

        // Act
        var keys = UpdateRateCountersAction.GetCounterKeys(ev, true, true, plan);

        // Assert
        Assert.Equal(10, keys.Count);
        Assert.Equal(10, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Compile_InvalidDefinitions_AreExcludedFromHotPathPlan()
    {
        // Arrange
        var disabled = CreateRule("disabled", RateNotificationSubject.Project);
        disabled.IsEnabled = false;
        var deleted = CreateRule("deleted", RateNotificationSubject.Project);
        deleted.IsDeleted = true;
        var wrongProject = CreateRule("wrong-project", RateNotificationSubject.Project);
        wrongProject.ProjectId = "project-2";
        var undefinedSignal = CreateRule("undefined-signal", RateNotificationSubject.Project);
        undefinedSignal.Signal = (RateNotificationSignal)Int32.MaxValue;
        var stackWithoutId = CreateRule("stack-without-id", RateNotificationSubject.Stack);
        var projectWithStack = CreateRule("project-with-stack", RateNotificationSubject.Project, "stack-1");

        // Act
        var plan = RateNotificationCounterPlan.Compile("project-1", [disabled, deleted, wrongProject, undefinedSignal, stackWithoutId, projectWithStack]);

        // Assert
        Assert.Equal(0, plan.RuleCount);
        Assert.False(plan.HasCounters);
    }

    [Fact]
    public void ShouldIncrement_EnabledPremiumContext_ReturnsTrue()
    {
        var context = CreateContext();

        Assert.True(UpdateRateCountersAction.ShouldIncrement(context));
    }

    [Fact]
    public void ShouldIncrement_MissingPremiumOrFeature_ReturnsFalse()
    {
        var context = CreateContext();
        context.Organization.HasPremiumFeatures = false;
        Assert.False(UpdateRateCountersAction.ShouldIncrement(context));

        context.Organization.HasPremiumFeatures = true;
        context.Organization.Features.Clear();
        Assert.False(UpdateRateCountersAction.ShouldIncrement(context));
    }

    [Fact]
    public void ShouldIncrement_SuppressedStackOrContext_ReturnsFalse()
    {
        var context = CreateContext();
        context.Stack!.Status = StackStatus.Ignored;
        Assert.False(UpdateRateCountersAction.ShouldIncrement(context));

        context = CreateContext();
        context.IsCancelled = true;
        Assert.False(UpdateRateCountersAction.ShouldIncrement(context));

        context = CreateContext();
        context.IsDiscarded = true;
        Assert.False(UpdateRateCountersAction.ShouldIncrement(context));
    }

    [Fact]
    public void ShouldIncrement_BotMarkedRequest_ReturnsFalse()
    {
        var context = CreateContext();
        context.Event.Data = new DataDictionary
        {
            [Event.KnownDataKeys.RequestInfo] = new RequestInfo
            {
                Data = new DataDictionary { [RequestInfo.KnownDataKeys.IsBot] = true }
            }
        };

        Assert.False(UpdateRateCountersAction.ShouldIncrement(context));
    }

    private static RateNotificationRule CreateRule(
        string id,
        RateNotificationSubject subject,
        string? stackId = null,
        RateNotificationSignal signal = RateNotificationSignal.Errors)
    {
        return new RateNotificationRule
        {
            Id = id,
            OrganizationId = "organization-1",
            ProjectId = "project-1",
            UserId = "user-1",
            Name = id,
            IsEnabled = true,
            Signal = signal,
            Subject = subject,
            StackId = stackId,
            Threshold = 1,
            Window = TimeSpan.FromMinutes(5),
            Cooldown = TimeSpan.FromMinutes(5),
            Version = 1
        };
    }

    private static EventContext CreateContext()
    {
        var organization = new Organization
        {
            Id = "organization-1",
            HasPremiumFeatures = true,
            Features = new HashSet<string> { OrganizationExtensions.RateNotificationsFeature }
        };
        var project = new Project { Id = "project-1", OrganizationId = organization.Id };
        var context = new EventContext(new PersistentEvent { Type = Event.KnownTypes.Error }, organization, project)
        {
            Stack = new Stack
            {
                Id = "stack-1",
                OrganizationId = organization.Id,
                ProjectId = project.Id,
                Status = StackStatus.Open
            }
        };
        context.Event.StackId = context.Stack.Id;
        return context;
    }
}
