using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;

namespace Exceptionless.Tests.Pipeline;

public sealed class EnvironmentManagementPipelineTests : IntegrationTestsBase
{
    private readonly EventPipeline _pipeline;
    private readonly IEventRepository _eventRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IWebHookRepository _webHookRepository;
    private readonly IQueue<EventNotification> _notificationQueue;
    private readonly IQueue<WebHookNotification> _webHookQueue;
    private readonly DiscardedEnvironmentService _discardedEnvironments;
    private readonly EventData _eventData;

    public EnvironmentManagementPipelineTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _pipeline = GetService<EventPipeline>();
        _eventRepository = GetService<IEventRepository>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _stackRepository = GetService<IStackRepository>();
        _webHookRepository = GetService<IWebHookRepository>();
        _notificationQueue = GetService<IQueue<EventNotification>>();
        _webHookQueue = GetService<IQueue<WebHookNotification>>();
        _discardedEnvironments = GetService<DiscardedEnvironmentService>();
        _eventData = GetService<EventData>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task RunAsync_WithDefaultProject_AcceptsEveryEnvironmentAndNotifiesOnlyForTheNewStack()
    {
        // Arrange
        await AddWebHookAsync();
        var project = await ConfigureProjectAsync(_ => { });

        // Act
        var first = await RunAsync(project, "Anything");
        var second = await RunAsync(project, "other");
        var third = await RunAsync(project, null);

        // Assert
        Assert.All(new[] { first, second, third }, c => Assert.True(c.IsProcessed));
        Assert.False(first.Stack!.IsNewNotificationPending);
        var notifications = await DequeueNotificationsAsync();
        Assert.Equal(first.Event.Id, Assert.Single(notifications).EventId);
        Assert.True(notifications[0].IsNew);
        Assert.Equal(1, (await _webHookQueue.GetQueueStatsAsync()).Enqueued);
        Assert.Empty((await _projectRepository.GetByIdAsync(project.Id))!.Environments);
    }

    [Fact]
    public async Task RunAsync_WithUnlistedEnvironmentAndDiscardEnabled_DiscardsBeforeStackingAndRecordsTheName()
    {
        // Arrange
        var project = await ConfigureProjectAsync(p =>
        {
            p.DiscardUnlistedEnvironments = true;
            p.Environments = [new ProjectEnvironment { Name = "Production" }];
        });

        // Act
        var typo = await RunAsync(project, "Prodution");
        var typoAgain = await RunAsync(project, "PRODUTION");
        var listed = await RunAsync(project, "PRODUCTION");
        await RefreshDataAsync();

        // Assert
        Assert.True(typo.IsDiscarded);
        Assert.True(typo.IsCancelled);
        Assert.False(typo.IsProcessed);
        Assert.Null(typo.Stack);
        Assert.True(typoAgain.IsDiscarded);
        Assert.True(listed.IsProcessed);
        Assert.False(listed.IsDiscarded);
        Assert.Equal(1, await _stackRepository.CountAsync());
        Assert.Equal(1, await _eventRepository.CountAsync());
        var discarded = Assert.Single((await _discardedEnvironments.GetAsync(project.Id)).Environments);
        Assert.Equal("prodution", discarded.Key);
        Assert.Equal("Prodution", discarded.Name);
        Assert.Equal(2, discarded.Count);
        Assert.Equal(["Production"], (await _projectRepository.GetByIdAsync(project.Id))!.Environments.Select(e => e.Name));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task RunAsync_WithoutEnvironment_DiscardsOnlyWhenEventsWithoutEnvironmentAreDiscarded(bool discardWithoutEnvironment, bool expectedDiscard)
    {
        // Arrange
        var project = await ConfigureProjectAsync(p =>
        {
            p.DiscardUnlistedEnvironments = true;
            p.DiscardEventsWithoutEnvironment = discardWithoutEnvironment;
            p.Environments = [new ProjectEnvironment { Name = "production" }];
        });

        // Act
        var context = await RunAsync(project, null);
        var discarded = (await _discardedEnvironments.GetAsync(project.Id)).Environments;

        // Assert
        Assert.Equal(expectedDiscard, context.IsDiscarded);
        Assert.Equal(!expectedDiscard, context.IsProcessed);
        var unspecified = discarded.SingleOrDefault();
        Assert.Equal(expectedDiscard, unspecified is not null);
        Assert.Null(unspecified?.Key);
        Assert.Null(unspecified?.Name);
    }

    [Fact]
    public async Task RunAsync_WithDiscardWithoutEnvironmentAndSessionHeartbeat_DoesNotDiscardTheHeartbeat()
    {
        // Arrange
        var project = await ConfigureProjectAsync(p =>
        {
            p.DiscardUnlistedEnvironments = true;
            p.DiscardEventsWithoutEnvironment = true;
        });
        var heartbeat = new EventContext(NewEvent(null, "heartbeat", Event.KnownTypes.SessionHeartbeat), await GetOrganizationAsync(), project);
        var log = new EventContext(NewEvent(null, "log"), await GetOrganizationAsync(), project);

        // Act
        await GetService<CheckEventEnvironmentAction>().ProcessBatchAsync([heartbeat, log]);

        // Assert
        Assert.False(heartbeat.IsDiscarded);
        Assert.True(log.IsDiscarded);
    }

    [Fact]
    public async Task RunAsync_WhenFirstSeenInDevelopmentThenProduction_NotifiesAsNewOnlyForTheFirstProductionEvent()
    {
        // Arrange
        await AddWebHookAsync();
        var project = await ConfigureProjectAsync(p => p.Environments =
        [
            new ProjectEnvironment { Name = "dev", Notify = false },
            new ProjectEnvironment { Name = "Production", Notify = true }
        ]);

        // Act
        var dev = await RunAsync(project, "DEV");
        var devAgain = await RunAsync(project, "dev");
        var devNotifications = await DequeueNotificationsAsync();
        var devWebHooks = (await _webHookQueue.GetQueueStatsAsync()).Enqueued;
        var production = await RunAsync(project, "production");
        var productionNotifications = await DequeueNotificationsAsync();
        var productionAgain = await RunAsync(project, "PRODUCTION");
        var laterNotifications = await DequeueNotificationsAsync();

        // Assert
        Assert.True(dev.IsNew);
        Assert.True(dev.Stack!.IsNewNotificationPending);
        Assert.False(devAgain.IsNew);
        Assert.Empty(devNotifications);
        Assert.Equal(0, devWebHooks);

        Assert.False(production.IsNew);
        var notification = Assert.Single(productionNotifications);
        Assert.Equal(production.Event.Id, notification.EventId);
        Assert.True(notification.IsNew);
        Assert.Equal(1, (await _webHookQueue.GetQueueStatsAsync()).Enqueued);
        Assert.False((await _stackRepository.GetByIdAsync(production.Event.StackId))!.IsNewNotificationPending);

        Assert.True(productionAgain.IsProcessed);
        Assert.Empty(laterNotifications);
        Assert.Equal(1, (await _webHookQueue.GetQueueStatsAsync()).Enqueued);
    }

    [Fact]
    public async Task RunAsync_WhenProductionEventsArriveConcurrentlyForPendingStack_NotifiesAsNewExactlyOnce()
    {
        // Arrange
        var project = await ConfigureProjectAsync(p => p.Environments =
        [
            new ProjectEnvironment { Name = "dev", Notify = false },
            new ProjectEnvironment { Name = "production" }
        ]);
        await RunAsync(project, "dev");

        // Act
        var contexts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => RunAsync(project, "production")));
        var notifications = await DequeueNotificationsAsync();

        // Assert
        Assert.All(contexts, c => Assert.True(c.IsProcessed));
        Assert.Single(notifications);
        Assert.True(notifications[0].IsNew);
    }

    [Fact]
    public async Task RunAsync_WhenNewStackStartsInNotifyingEnvironment_NotifiesOnceAndNeverMarksItPending()
    {
        // Arrange
        var project = await ConfigureProjectAsync(p => p.Environments =
        [
            new ProjectEnvironment { Name = "dev", Notify = false },
            new ProjectEnvironment { Name = "production" }
        ]);

        // Act
        var production = await RunAsync(project, "production");
        var dev = await RunAsync(project, "dev");
        var laterProduction = await RunAsync(project, "production");
        var notifications = await DequeueNotificationsAsync();

        // Assert
        Assert.False(production.Stack!.IsNewNotificationPending);
        var notification = Assert.Single(notifications);
        Assert.Equal(production.Event.Id, notification.EventId);
        Assert.True(dev.IsProcessed);
        Assert.True(laterProduction.IsProcessed);
    }

    [Fact]
    public async Task RunAsync_WithLegacyStackFromBeforeTheField_NeverNotifiesAsNew()
    {
        // Arrange
        var project = await ConfigureProjectAsync(p => p.Environments =
        [
            new ProjectEnvironment { Name = "dev", Notify = false },
            new ProjectEnvironment { Name = "production" }
        ]);
        var existing = await RunAsync(project, "dev");
        Assert.True(existing.Stack!.IsNewNotificationPending);

        // Stacks created before the field existed have no value in the index at all.
        await _stackRepository.PatchAsync(existing.Stack.Id, new ScriptPatch("ctx._source.remove('is_new_notification_pending');"), o => o.ImmediateConsistency());
        var legacy = await _stackRepository.GetByIdAsync(existing.Stack.Id);

        // Act
        var production = await RunAsync(project, "production");
        var dev = await RunAsync(project, "dev");
        var notifications = await DequeueNotificationsAsync();

        // Assert
        Assert.False(legacy!.IsNewNotificationPending);
        Assert.True(production.IsProcessed);
        Assert.True(dev.IsProcessed);
        Assert.Empty(notifications);
    }

    [Fact]
    public async Task RunAsync_WithCriticalEvent_NotifiesOnlyFromNotifyingEnvironments()
    {
        // Arrange
        var project = await ConfigureProjectAsync(p =>
        {
            p.NotifyOtherEnvironments = false;
            p.Environments =
            [
                new ProjectEnvironment { Name = "dev", Notify = false },
                new ProjectEnvironment { Name = "production" }
            ];
        });
        await RunAsync(project, "production", source: "warmup");
        await DequeueNotificationsAsync();

        // Act
        var dev = await RunAsync(project, "dev", critical: true);
        var unlisted = await RunAsync(project, "qa", critical: true);
        var unspecified = await RunAsync(project, null, critical: true);
        var devNotifications = await DequeueNotificationsAsync();
        var production = await RunAsync(project, "PRODUCTION", critical: true);
        var productionNotifications = await DequeueNotificationsAsync();

        // Assert
        Assert.All(new[] { dev, unlisted, unspecified, production }, c => Assert.True(c.IsProcessed));
        Assert.Empty(devNotifications);
        var notification = Assert.Single(productionNotifications);
        Assert.Equal(production.Event.Id, notification.EventId);
        Assert.False(notification.IsRegression);
    }

    [Fact]
    public async Task RunAsync_WithNotifyOtherEnvironments_NotifiesForUnlistedAndUnspecifiedEnvironments()
    {
        // Arrange
        var project = await ConfigureProjectAsync(p => p.Environments = [new ProjectEnvironment { Name = "dev", Notify = false }]);

        // Act
        await RunAsync(project, "qa", source: "one");
        await RunAsync(project, null, source: "two");
        await RunAsync(project, "dev", source: "three");
        var notifications = await DequeueNotificationsAsync();

        // Assert
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, n => Assert.True(n.IsNew));
    }

    [Fact]
    public async Task RunAsync_WithRegression_NotifiesOnlyWhenTheEnvironmentNotifies()
    {
        // Arrange
        var project = await ConfigureProjectAsync(p => p.Environments =
        [
            new ProjectEnvironment { Name = "dev", Notify = false },
            new ProjectEnvironment { Name = "production" }
        ]);
        var first = await RunAsync(project, "production");
        await DequeueNotificationsAsync();
        var stack = await _stackRepository.GetByIdAsync(first.Event.StackId);
        stack!.Status = StackStatus.Fixed;
        stack.DateFixed = DateTime.UtcNow.AddHours(-1);
        await _stackRepository.SaveAsync(stack, o => o.ImmediateConsistency());

        // Act
        var dev = await RunAsync(project, "dev");
        var devNotifications = await DequeueNotificationsAsync();
        var devStack = await _stackRepository.GetByIdAsync(first.Event.StackId);
        devStack!.Status = StackStatus.Fixed;
        devStack.DateFixed = DateTime.UtcNow.AddHours(-1);
        await _stackRepository.SaveAsync(devStack, o => o.ImmediateConsistency());
        var production = await RunAsync(project, "production");
        var productionNotifications = await DequeueNotificationsAsync();

        // Assert
        Assert.True(dev.IsRegression);
        Assert.Empty(devNotifications);
        Assert.True(production.IsRegression);
        var notification = Assert.Single(productionNotifications);
        Assert.True(notification.IsRegression);
    }

    [Fact]
    public async Task RunAsync_WhenAStaleStackSaveRestoresTheClaimedFlag_DoesNotNotifyAsNewTwice()
    {
        // Arrange
        var project = await ConfigureProjectAsync(p => p.Environments =
        [
            new ProjectEnvironment { Name = "dev", Notify = false },
            new ProjectEnvironment { Name = "production" }
        ]);
        var dev = await RunAsync(project, "dev");
        var stale = (await _stackRepository.GetByIdAsync(dev.Event.StackId))!;
        Assert.True(stale.IsNewNotificationPending);
        await RunAsync(project, "production");
        Assert.Single(await DequeueNotificationsAsync());

        // Act
        await _stackRepository.SaveAsync(stale, o => o.ImmediateConsistency());
        Assert.True((await _stackRepository.GetByIdAsync(stale.Id))!.IsNewNotificationPending);
        await RunAsync(project, "production");
        var notifications = await DequeueNotificationsAsync();

        // Assert
        Assert.Empty(notifications);
        Assert.False((await _stackRepository.GetByIdAsync(stale.Id))!.IsNewNotificationPending);
    }

    [Fact]
    public async Task RunAsync_WhenAStackRegressesInANonNotifyingEnvironment_NotifiesOnTheFirstNotifyingOccurrence()
    {
        // Arrange
        var project = await ConfigureProjectAsync(p => p.Environments =
        [
            new ProjectEnvironment { Name = "dev", Notify = false },
            new ProjectEnvironment { Name = "production" }
        ]);
        var first = await RunAsync(project, "production");
        await DequeueNotificationsAsync();
        await MarkFixedAsync(first.Event.StackId);

        // Act
        var dev = await RunAsync(project, "dev");
        var devNotifications = await DequeueNotificationsAsync();
        var pendingStack = (await _stackRepository.GetByIdAsync(first.Event.StackId))!;
        var production = await RunAsync(project, "production");
        var productionNotifications = await DequeueNotificationsAsync();
        await RunAsync(project, "production");
        var laterNotifications = await DequeueNotificationsAsync();

        // Assert
        Assert.True(dev.IsRegression);
        Assert.Empty(devNotifications);
        Assert.Equal(StackStatus.Regressed, pendingStack.Status);
        Assert.True(pendingStack.IsRegressionNotificationPending);
        Assert.False(production.IsRegression);
        var notification = Assert.Single(productionNotifications);
        Assert.True(notification.IsRegression);
        Assert.False(notification.IsNew);
        Assert.Empty(laterNotifications);
        Assert.False((await _stackRepository.GetByIdAsync(first.Event.StackId))!.IsRegressionNotificationPending);
    }

    [Fact]
    public async Task RunAsync_WhenABatchWithAnExplicitStackIdRegressesInDevThenProduction_NotifiesOnceForProduction()
    {
        // Arrange
        var project = await ConfigureProjectAsync(p => p.Environments =
        [
            new ProjectEnvironment { Name = "dev", Notify = false },
            new ProjectEnvironment { Name = "production" }
        ]);
        var first = await RunAsync(project, "production");
        await DequeueNotificationsAsync();
        await MarkFixedAsync(first.Event.StackId);
        var dev = NewEvent("dev", "test");
        dev.StackId = first.Event.StackId;
        var production = NewEvent("production", "test");
        production.StackId = first.Event.StackId;
        production.Date = dev.Date.AddSeconds(1);

        // Act
        var contexts = await _pipeline.RunAsync([dev, production], await GetOrganizationAsync(), project);
        await RefreshDataAsync();
        var notifications = await DequeueNotificationsAsync();

        // Assert
        Assert.NotSame(contexts.First().Stack, contexts.Last().Stack);
        var notification = Assert.Single(notifications);
        Assert.Equal(production.Id, notification.EventId);
        Assert.True(notification.IsRegression);
        Assert.False((await _stackRepository.GetByIdAsync(first.Event.StackId))!.IsRegressionNotificationPending);
    }

    [Fact]
    public async Task RunAsync_WhenAStackRegressesInANotifyingEnvironment_NotifiesImmediatelyAndNeverMarksItPending()
    {
        // Arrange
        var project = await ConfigureProjectAsync(_ => { });
        var first = await RunAsync(project, "production");
        await DequeueNotificationsAsync();
        await MarkFixedAsync(first.Event.StackId);

        // Act
        var regression = await RunAsync(project, "production");
        var notifications = await DequeueNotificationsAsync();
        await RunAsync(project, "production");
        var laterNotifications = await DequeueNotificationsAsync();

        // Assert
        Assert.True(regression.IsRegression);
        Assert.Equal(regression.Event.Id, Assert.Single(notifications).EventId);
        Assert.True(notifications[0].IsRegression);
        Assert.False((await _stackRepository.GetByIdAsync(first.Event.StackId))!.IsRegressionNotificationPending);
        Assert.Empty(laterNotifications);
    }

    [Fact]
    public async Task RunAsync_WhenTheStatusChangesWhileARegressionIsPending_DropsThePendingNotification()
    {
        // Arrange
        var project = await ConfigureProjectAsync(p => p.Environments =
        [
            new ProjectEnvironment { Name = "dev", Notify = false },
            new ProjectEnvironment { Name = "production" }
        ]);
        var first = await RunAsync(project, "production");
        await DequeueNotificationsAsync();
        await MarkFixedAsync(first.Event.StackId);
        await RunAsync(project, "dev");
        var stack = (await _stackRepository.GetByIdAsync(first.Event.StackId))!;
        Assert.True(stack.IsRegressionNotificationPending);

        // Act
        stack.MarkOpen();
        await _stackRepository.SaveAsync(stack, o => o.ImmediateConsistency());
        await RunAsync(project, "production");
        var notifications = await DequeueNotificationsAsync();

        // Assert
        Assert.Empty(notifications);
        Assert.False((await _stackRepository.GetByIdAsync(first.Event.StackId))!.IsRegressionNotificationPending);
    }

    [Fact]
    public async Task RunAsync_WhenTwoBatchesTouchAPendingStack_TagsUpdateDoesNotRestoreAClaimedFlag()
    {
        // Arrange
        var project = await ConfigureProjectAsync(p => p.Environments =
        [
            new ProjectEnvironment { Name = "dev", Notify = false },
            new ProjectEnvironment { Name = "production" }
        ]);
        var dev = await RunAsync(project, "dev");
        var staleBatchStack = (await _stackRepository.GetByIdAsync(dev.Event.StackId))!;
        await RunAsync(project, "production");
        await DequeueNotificationsAsync();

        // Act
        staleBatchStack.Tags.Add("late");
        await _stackRepository.UpdateTagsAsync(staleBatchStack);
        await RefreshDataAsync();

        // Assert
        var stack = (await _stackRepository.GetByIdAsync(dev.Event.StackId))!;
        Assert.False(stack.IsNewNotificationPending);
        Assert.Contains("late", stack.Tags);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenAnotherBatchClaimsAfterTheStackWasLoaded_AddingTagsDoesNotRestoreTheClaimedFlag()
    {
        // Arrange
        var project = await ConfigureProjectAsync(p => p.Environments =
        [
            new ProjectEnvironment { Name = "dev", Notify = false },
            new ProjectEnvironment { Name = "production" }
        ]);
        var dev = await RunAsync(project, "dev");
        Assert.True(dev.Stack!.IsNewNotificationPending);

        // The batch loads the pending stack, then another batch claims the notification before this one saves its tags.
        var repository = DispatchProxy.Create<IStackRepository, AfterStackLoadedProxy>();
        var proxy = (AfterStackLoadedProxy)(object)repository;
        proxy.Inner = _stackRepository;
        proxy.AfterLoaded = loaded => _stackRepository.TryClaimNotificationAsync(loaded, PendingStackNotification.New);
        var action = ActivatorUtilities.CreateInstance<AssignToStackAction>(GetService<IServiceProvider>(), repository);

        var ev = NewEvent("dev", "boom");
        ev.Tags = ["late"];
        var context = new EventContext(ev, await GetOrganizationAsync(), project);
        foreach (var item in dev.Stack.SignatureInfo)
            context.StackSignatureData[item.Key] = item.Value;

        // Act
        await action.ProcessBatchAsync([context]);
        await RefreshDataAsync();

        // Assert
        Assert.Equal(dev.Stack.Id, context.Stack!.Id);
        var stack = (await _stackRepository.GetByIdAsync(dev.Stack.Id))!;
        Assert.Contains("late", stack.Tags);
        Assert.False(stack.IsNewNotificationPending);
    }

    private class AfterStackLoadedProxy : DispatchProxy
    {
        public IStackRepository Inner { get; set; } = null!;
        public Func<Stack, Task<bool>> AfterLoaded { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            object? result = targetMethod!.Invoke(Inner, args);
            return targetMethod.Name == nameof(IStackRepository.GetStackBySignatureHashAsync) ? LoadAsync((Task<Stack?>)result!) : result;
        }

        private async Task<Stack?> LoadAsync(Task<Stack?> load)
        {
            var stack = await load;
            if (stack is not null)
                await AfterLoaded(stack);

            return stack;
        }
    }

    private async Task MarkFixedAsync(string stackId)
    {
        var stack = (await _stackRepository.GetByIdAsync(stackId))!;
        stack.MarkFixed(null, TimeProvider);
        stack.DateFixed = DateTime.UtcNow.AddHours(-1);
        await _stackRepository.SaveAsync(stack, o => o.ImmediateConsistency());
    }

    [Fact]
    public async Task TryClaimNewNotificationAsync_WithConcurrentCallers_ReturnsTrueForExactlyOne()
    {
        // Arrange
        var stack = await _stackRepository.AddAsync(GetService<StackData>().GenerateStack(projectId: SampleDataService.TEST_PROJECT_ID, organizationId: SampleDataService.TEST_ORG_ID), o => o.ImmediateConsistency());
        stack.IsNewNotificationPending = true;
        stack = await _stackRepository.SaveAsync(stack, o => o.ImmediateConsistency());

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => _stackRepository.TryClaimNotificationAsync(stack, PendingStackNotification.New)));
        bool claimedAgain = await _stackRepository.TryClaimNotificationAsync(stack, PendingStackNotification.New);

        // Assert
        Assert.Equal(1, results.Count(r => r));
        Assert.False(claimedAgain);
        Assert.False((await _stackRepository.GetByIdAsync(stack.Id))!.IsNewNotificationPending);
    }

    [Fact]
    public async Task TryClaimNewNotificationAsync_WithStackThatIsNotPending_ReturnsFalse()
    {
        // Arrange
        var stack = await _stackRepository.AddAsync(GetService<StackData>().GenerateStack(projectId: SampleDataService.TEST_PROJECT_ID, organizationId: SampleDataService.TEST_ORG_ID), o => o.ImmediateConsistency());

        // Act
        bool claimed = await _stackRepository.TryClaimNotificationAsync(stack, PendingStackNotification.New);

        // Assert
        Assert.False(claimed);
    }

    private async Task<EventContext> RunAsync(Project project, string? environment, string source = "test", bool critical = false)
    {
        var ev = NewEvent(environment, source);
        if (critical)
            ev.Tags!.Add(Event.KnownTags.Critical);

        var context = await _pipeline.RunAsync(ev, await GetOrganizationAsync(), project);
        await RefreshDataAsync();
        return context;
    }

    private PersistentEvent NewEvent(string? environment, string source, string type = Event.KnownTypes.Error)
    {
        var ev = _eventData.GenerateEvent(organizationId: SampleDataService.TEST_ORG_ID, projectId: SampleDataService.TEST_PROJECT_ID, type: type, source: source, generateData: false, generateTags: false, occurrenceDate: DateTimeOffset.Now);
        ev.Tags = [];
        ev.Environment = environment;
        ev.Data = new DataDictionary { { Event.KnownDataKeys.SimpleError, new SimpleError { Message = "Boom", Type = $"Test.{source}Exception", StackTrace = $"at {source}()" } } };
        return ev;
    }

    private async Task<Organization> GetOrganizationAsync() => (await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID))!;

    private async Task<Project> ConfigureProjectAsync(Action<Project> configure)
    {
        var project = (await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID))!;
        configure(project);
        return await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency().Cache());
    }

    private Task AddWebHookAsync()
    {
        return _webHookRepository.AddAsync(new WebHook
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = SampleDataService.TEST_PROJECT_ID,
            Url = "https://example.com/hook",
            Version = WebHook.KnownVersions.Version2,
            EventTypes = [WebHook.KnownEventTypes.NewError, WebHook.KnownEventTypes.CriticalError, WebHook.KnownEventTypes.StackRegression],
            IsEnabled = true
        }, o => o.ImmediateConsistency());
    }

    private async Task<List<EventNotification>> DequeueNotificationsAsync()
    {
        var notifications = new List<EventNotification>();
        while (await _notificationQueue.DequeueAsync(TimeSpan.Zero) is { } entry)
        {
            notifications.Add(entry.Value);
            await entry.CompleteAsync();
        }

        return notifications;
    }
}
