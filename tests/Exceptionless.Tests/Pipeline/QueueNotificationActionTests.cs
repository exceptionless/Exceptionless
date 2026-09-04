using System.Reflection;
using System.Runtime.ExceptionServices;
using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Queues;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Exceptionless.Tests.Pipeline;

public sealed class QueueNotificationActionTests : TestWithServices
{
    private readonly QueueNotificationAction _action;
    private readonly WebHookRepositoryProxy _webHookRepository;
    private readonly IQueue<EventNotification> _notificationQueue;
    private readonly IQueue<WebHookNotification> _webHookNotificationQueue;

    public QueueNotificationActionTests(ITestOutputHelper output) : base(output)
    {
        IWebHookRepository webHookRepository = WebHookRepositoryProxy.Create(
            GetService<IWebHookRepository>(),
            out _webHookRepository);
        _notificationQueue = GetService<IQueue<EventNotification>>();
        _webHookNotificationQueue = GetService<IQueue<WebHookNotification>>();
        _action = new QueueNotificationAction(
            _notificationQueue,
            _webHookNotificationQueue,
            webHookRepository,
            GetService<WebHookDataPluginManager>(),
            GetService<AppOptions>(),
            GetService<ILoggerFactory>());
    }

    [Fact]
    public async Task ProcessIngestionV3BatchAsync_SameOrganizationAndProject_LoadsWebHooksOnce()
    {
        var organization = CreateOrganization("organization-a");
        var project = CreateProject(organization.Id, "project-a");
        EventContext[] contexts =
        [
            CreateContext("event-a", organization, project),
            CreateContext("event-b", organization, project),
            CreateContext("event-c", organization, project)
        ];

        await _action.ProcessIngestionV3BatchAsync(contexts);

        Assert.Equal(1, _webHookRepository.LoadCount);
    }

    [Fact]
    public async Task ProcessIngestionV3BatchAsync_MultipleOrganizationProjectPairs_LoadsEachOnce()
    {
        var firstOrganization = CreateOrganization("organization-a");
        var firstProject = CreateProject(firstOrganization.Id, "project-a");
        var secondOrganization = CreateOrganization("organization-b");
        var secondProject = CreateProject(secondOrganization.Id, "project-b");
        EventContext[] contexts =
        [
            CreateContext("event-a", firstOrganization, firstProject),
            CreateContext("event-b", firstOrganization, firstProject),
            CreateContext("event-c", secondOrganization, secondProject)
        ];

        await _action.ProcessIngestionV3BatchAsync(contexts);

        Assert.Equal(2, _webHookRepository.LoadCount);
    }

    [Fact]
    public async Task ProcessIngestionV3BatchAsync_WebHookLoadFails_PropagatesForRetry()
    {
        var organization = CreateOrganization("organization-a");
        var project = CreateProject(organization.Id, "project-a");
        _webHookRepository.LoadException = new InvalidOperationException("failed");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _action.ProcessIngestionV3BatchAsync([CreateContext("event-a", organization, project)]));

        Assert.Equal("failed", exception.Message);
    }

    [Fact]
    public async Task ProcessAsync_V2Notifications_UsesLegacyDeterministicIdentifiersWithoutDurableClaims()
    {
        var organization = CreateOrganization("organization-v2");
        var project = CreateProject(organization.Id, "project-v2");
        project.NotificationSettings["user-1"] = new NotificationSettings { ReportNewErrors = true };
        var hook = new WebHook
        {
            Id = "webhook-v2",
            OrganizationId = organization.Id,
            ProjectId = project.Id,
            Url = "https://example.com/webhook",
            EventTypes = [WebHook.KnownEventTypes.NewError],
            Version = WebHook.KnownVersions.Version2
        };
        _webHookRepository.Results = new FindResults<WebHook>(
            [new FindHit<WebHook>(hook.Id, hook, 1)],
            1);
        EventContext context = CreateContext("event-v2", organization, project, isIngestionV3: false);
        context.IsNew = true;

        await _action.ProcessAsync(context);

        var eventEntry = await _notificationQueue.DequeueAsync(TestCancellationToken);
        var webHookEntry = await _webHookNotificationQueue.DequeueAsync(TestCancellationToken);
        Assert.NotNull(eventEntry);
        Assert.NotNull(webHookEntry);
        Assert.Equal("event-notification:event-v2", eventEntry.Value.DeduplicationId);
        Assert.False(eventEntry.Value.UseDurableDeduplication);
        Assert.Equal("event-webhook:event-v2:webhook-v2:General", webHookEntry.Value.DeduplicationId);
        Assert.False(webHookEntry.Value.UseDurableDeduplication);
    }

    private static Organization CreateOrganization(string id) => new()
    {
        Id = id,
        Name = id,
        HasPremiumFeatures = true
    };

    private static Project CreateProject(string organizationId, string id) => new()
    {
        Id = id,
        OrganizationId = organizationId,
        Name = id
    };

    private static EventContext CreateContext(string eventId, Organization organization, Project project, bool isIngestionV3 = true)
    {
        var persistentEvent = new PersistentEvent
        {
            Id = eventId,
            OrganizationId = organization.Id,
            ProjectId = project.Id,
            StackId = String.Concat("stack-", eventId),
            Type = Event.KnownTypes.Error
        };

        return new EventContext(persistentEvent, organization, project)
        {
            IsIngestionV3 = isIngestionV3,
            Stack = new Stack
            {
                Id = persistentEvent.StackId,
                OrganizationId = organization.Id,
                ProjectId = project.Id,
                Type = persistentEvent.Type,
                Status = StackStatus.Open,
                TotalOccurrences = 7
            }
        };
    }

    private class WebHookRepositoryProxy : DispatchProxy
    {
        private IWebHookRepository _inner = null!;

        public int LoadCount { get; private set; }
        public Exception? LoadException { get; set; }
        public FindResults<WebHook>? Results { get; set; }

        public static IWebHookRepository Create(IWebHookRepository inner, out WebHookRepositoryProxy implementation)
        {
            IWebHookRepository proxy = DispatchProxy.Create<IWebHookRepository, WebHookRepositoryProxy>();
            implementation = (WebHookRepositoryProxy)(object)proxy;
            implementation._inner = inner;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (String.Equals(targetMethod.Name, nameof(IWebHookRepository.GetByOrganizationIdOrProjectIdAsync), StringComparison.Ordinal))
            {
                LoadCount++;
                if (LoadException is not null)
                    return Task.FromException<FindResults<WebHook>>(LoadException);
                return Task.FromResult(Results ?? FindResults<WebHook>.Empty);
            }

            try
            {
                return targetMethod.Invoke(_inner, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
