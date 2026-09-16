using System.Reflection;
using System.Text.Json.Nodes;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Foundatio.Caching;
using Foundatio.Queues;
using Foundatio.Repositories.Models;
using Foundatio.Resilience;
using Foundatio.Serializer;
using Foundatio.Utility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public sealed class EventPostRetryTests : TestWithServices
{
    private const string OrganizationId = "retry-organization";
    private const string ProjectId = "retry-project";

    private readonly EventPostsJob _job;
    private readonly EventPostService _eventPostService;
    private readonly OneTimeFailingEventPipeline _pipeline;
    private readonly EventRepositoryProxy _eventRepository;
    private readonly UsageService _usageService;
    private readonly ITextSerializer _serializer;

    public EventPostRetryTests(ITestOutputHelper output) : base(output)
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        _job = GetService<EventPostsJob>();
        _eventPostService = GetService<EventPostService>();
        _pipeline = GetService<EventPipeline>() as OneTimeFailingEventPipeline
            ?? throw new InvalidOperationException("The test EventPipeline was not registered.");
        _eventRepository = (EventRepositoryProxy)(object)GetService<IEventRepository>();
        _usageService = GetService<UsageService>();
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public async Task SingleEventPipelineFailure_IsRetriedAndCommittedOnce()
    {
        var persistentEvent = new PersistentEvent
        {
            Type = Event.KnownTypes.Log,
            Message = "retry me",
            ReferenceId = "retry-reference",
            Date = TimeProvider.GetUtcNow()
        };

        await EnqueueEventPostAsync(persistentEvent);

        var firstResult = await _job.RunAsync(CancellationToken.None);
        Assert.True(firstResult.IsSuccess);
        Assert.Equal(1, _pipeline.RunCount);
        Assert.Empty(_eventRepository.SavedEvents);
        Assert.Equal(0, (await _usageService.GetUsageAsync(OrganizationId, ProjectId)).CurrentUsage.Total);

        var retryResult = await _job.RunAsync(CancellationToken.None);
        Assert.True(retryResult.IsSuccess);
        Assert.Equal(2, _pipeline.RunCount);
        Assert.Single(_eventRepository.SavedEvents);
        Assert.Equal(1, (await _usageService.GetUsageAsync(OrganizationId, ProjectId)).CurrentUsage.Total);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MixedBatch_CompletionSideEffectFails_PreservesRetriesAndUsage(bool failNotificationRead)
    {
        _pipeline.ProcessFirstEvent = true;
        var cache = (FailOnceCacheProxy)(object)GetService<ICacheClient>();
        cache.FailDiscardedIncrement = true;
        cache.FailCompletionNotificationRead = failNotificationRead;
        await EnqueueEventPostAsync(
            new PersistentEvent { Type = Event.KnownTypes.Log, ReferenceId = "processed", Date = TimeProvider.GetUtcNow() },
            new PersistentEvent { Type = Event.KnownTypes.Log, ReferenceId = "retry", Date = TimeProvider.GetUtcNow() });

        var firstResult = await _job.RunAsync(CancellationToken.None);
        Assert.Equal(!failNotificationRead, firstResult.IsSuccess);
        Assert.Single(_eventRepository.SavedEvents);
        Assert.Equal(1, (await _usageService.GetUsageAsync(OrganizationId, ProjectId)).CurrentUsage.Total);

        if (failNotificationRead)
            Assert.True((await _job.RunAsync(CancellationToken.None)).IsSuccess);
        Assert.Equal(1, _pipeline.RunCount);
        Assert.Equal(1, (await GetService<IQueue<EventPost>>().GetQueueStatsAsync()).Queued);
        Assert.True((await _job.RunAsync(CancellationToken.None)).IsSuccess);

        Assert.Equal(2, _pipeline.RunCount);
        Assert.Equal(["processed", "retry"], _eventRepository.SavedEvents.Select(ev => ev.ReferenceId));
        Assert.Equal(2, (await _usageService.GetUsageAsync(OrganizationId, ProjectId)).CurrentUsage.Total);
        Assert.Equal(1, (await _usageService.GetUsageAsync(OrganizationId, ProjectId)).CurrentUsage.Discarded);
        Assert.Equal(1, (await _usageService.GetUsageAsync(OrganizationId)).CurrentUsage.Discarded);
    }

    [Fact]
    public async Task PendingRetries_PartialEnqueueFailure_RedeliversOnlyUnacknowledgedIndexes()
    {
        var reservation = await CreateCompletedReservationAsync("partial", [1, 2]);
        var attempts = new List<int>();
        Assert.False(await _usageService.RetryPendingEventIngestAsync(reservation, index =>
        {
            attempts.Add(index);
            return Task.FromResult(index == 1);
        }));

        Assert.True(await _usageService.RetryPendingEventIngestAsync(reservation, index =>
        {
            attempts.Add(index);
            return Task.FromResult(true);
        }));
        Assert.True(await _usageService.RetryPendingEventIngestAsync(reservation, _ => throw new InvalidOperationException("Acknowledged retries must not be dispatched.")));
        Assert.Equal([1, 2, 2], attempts);
        Assert.Equal(1, (await _usageService.GetUsageAsync(OrganizationId, ProjectId)).CurrentUsage.Total);
    }

    [Fact]
    public async Task PendingRetries_ConcurrentDelivery_DispatchesEachIndexOnce()
    {
        var reservation = await CreateCompletedReservationAsync("concurrent", [1, 2]);
        var firstEnqueue = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEnqueue = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = new List<int>();
        var first = _usageService.RetryPendingEventIngestAsync(reservation, async index =>
        {
            attempts.Add(index);
            firstEnqueue.TrySetResult();
            await releaseEnqueue.Task;
            return true;
        });
        await firstEnqueue.Task;
        var second = _usageService.RetryPendingEventIngestAsync(reservation, _ => throw new InvalidOperationException("Concurrent delivery must observe acknowledgements."));
        releaseEnqueue.SetResult();

        Assert.All(await Task.WhenAll(first, second), Assert.True);
        Assert.Equal([1, 2], attempts);
    }

    [Fact]
    public async Task PendingRetries_AcknowledgementFailure_KeepsEventForAtLeastOnceDelivery()
    {
        var reservation = await CreateCompletedReservationAsync("acknowledgement", [1]);
        var cache = (FailOnceCacheProxy)(object)GetService<ICacheClient>();
        int attempts = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _usageService.RetryPendingEventIngestAsync(reservation, _ =>
        {
            attempts++;
            cache.FailReservationAcknowledgement = true;
            return Task.FromResult(true);
        }));

        Assert.True(await _usageService.RetryPendingEventIngestAsync(reservation, index =>
        {
            Assert.Equal(1, index);
            attempts++;
            return Task.FromResult(true);
        }));
        Assert.Equal(2, attempts);
        Assert.Equal(1, (await _usageService.GetUsageAsync(OrganizationId, ProjectId)).CurrentUsage.Total);
    }

    [Fact]
    public async Task CompletedReservation_LegacyRecordWithoutRetryIndexes_HasNoPendingRetries()
    {
        var reservation = await CreateCompletedReservationAsync("legacy", []);
        var cache = GetService<ICacheClient>();
        string key = $"usage:ingest-reservation:{{{OrganizationId}}}:legacy";
        var value = await cache.GetAsync<string>(key);
        var legacy = JsonNode.Parse(value.Value)!.AsObject();
        Assert.True(legacy.Remove("RetryIndexes"));
        await cache.SetAsync(key, legacy.ToJsonString(), TimeSpan.FromDays(35));

        Assert.True(await _usageService.RetryPendingEventIngestAsync(reservation, _ => throw new InvalidOperationException("Legacy completed records have no pending retries.")));
    }

    [Theory]
    [InlineData("[1,1]")]
    [InlineData("[99]")]
    [InlineData("null")]
    public async Task PendingRetries_InvalidStoredIndexes_RejectsBeforeDispatch(string indexes)
    {
        var reservation = await CreateCompletedReservationAsync("invalid", [1]);
        var cache = GetService<ICacheClient>();
        string key = $"usage:ingest-reservation:{{{OrganizationId}}}:invalid";
        var value = await cache.GetAsync<string>(key);
        var invalid = JsonNode.Parse(value.Value)!.AsObject();
        invalid["RetryIndexes"] = JsonNode.Parse(indexes);
        await cache.SetAsync(key, invalid.ToJsonString(), TimeSpan.FromDays(35));

        await Assert.ThrowsAsync<UsageServiceException>(() => _usageService.RetryPendingEventIngestAsync(reservation,
            _ => throw new InvalidOperationException("Invalid retry metadata must not dispatch events.")));
    }

    private async Task<EventIngestReservation> CreateCompletedReservationAsync(string id, int[] retryIndexes)
    {
        var organization = ((OrganizationRepositoryProxy)(object)GetService<IOrganizationRepository>()).Organization;
        var project = ((ProjectRepositoryProxy)(object)GetService<IProjectRepository>()).Project;
        var reservation = await _usageService.ReserveEventIngestAsync(organization, project, id,
            [new(0, 0), new(1, 1), new(2, 2)]);
        await _usageService.CompleteEventIngestReservationAsync(reservation, organization, 1, retryIndexes);
        return reservation;
    }

    protected override void RegisterServices(IServiceCollection services, AppOptions options)
    {
        base.RegisterServices(services, options);

        services.Replace(ServiceDescriptor.Singleton<ICacheClient>(serviceProvider =>
        {
            var cache = DispatchProxy.Create<ICacheClient, FailOnceCacheProxy>();
            ((FailOnceCacheProxy)(object)cache).Inner = new InMemoryCacheClient(new InMemoryCacheClientOptions
            {
                CloneValues = true,
                Serializer = serviceProvider.GetRequiredService<ISerializer>(),
                TimeProvider = serviceProvider.GetRequiredService<TimeProvider>(),
                LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>()
            });
            return cache;
        }));

        var organization = new Organization
        {
            Id = OrganizationId,
            Name = "Retry Organization",
            PlanId = "EX_SMALL",
            MaxEventsPerMonth = 100
        };
        var organizationRepository = DispatchProxy.Create<IOrganizationRepository, OrganizationRepositoryProxy>();
        ((OrganizationRepositoryProxy)(object)organizationRepository).Organization = organization;
        services.Replace(ServiceDescriptor.Singleton<IOrganizationRepository>(organizationRepository));

        var project = new Project
        {
            Id = ProjectId,
            OrganizationId = OrganizationId,
            Name = "Retry Project",
            NextSummaryEndOfDayTicks = DateTime.UtcNow.Ticks
        };
        var projectRepository = DispatchProxy.Create<IProjectRepository, ProjectRepositoryProxy>();
        ((ProjectRepositoryProxy)(object)projectRepository).Project = project;
        services.Replace(ServiceDescriptor.Singleton<IProjectRepository>(projectRepository));

        var eventRepository = DispatchProxy.Create<IEventRepository, EventRepositoryProxy>();
        services.Replace(ServiceDescriptor.Singleton<IEventRepository>(eventRepository));

        services.Replace(ServiceDescriptor.Singleton<EventPipeline, OneTimeFailingEventPipeline>());
        services.AddSingleton<EventPostsJob>();
        services.Replace(ServiceDescriptor.Singleton<IQueue<EventPost>>(serviceProvider => new InMemoryQueue<EventPost>(new InMemoryQueueOptions<EventPost>
        {
            RetryDelay = TimeSpan.Zero,
            Serializer = serviceProvider.GetRequiredService<ISerializer>(),
            TimeProvider = serviceProvider.GetRequiredService<TimeProvider>(),
            ResiliencePolicyProvider = serviceProvider.GetRequiredService<IResiliencePolicyProvider>(),
            LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>()
        })));
    }

    private async Task EnqueueEventPostAsync(params PersistentEvent[] events)
    {
        var eventPost = new EventPost(false)
        {
            OrganizationId = OrganizationId,
            ProjectId = ProjectId,
            ApiVersion = 2,
            CharSet = "utf-8",
            ContentEncoding = "gzip",
            MediaType = "application/json",
            UserAgent = "exceptionless-retry-test"
        };

        using var stream = new MemoryStream(_serializer.SerializeToBytes(events).Compress());
        Assert.NotNull(await _eventPostService.EnqueueAsync(eventPost, stream));
    }

    private sealed class OneTimeFailingEventPipeline : EventPipeline
    {
        private readonly IEventRepository _eventRepository;
        private int _runCount;

        public OneTimeFailingEventPipeline(IServiceProvider serviceProvider, AppOptions options, ILoggerFactory loggerFactory)
            : base(serviceProvider, options, loggerFactory)
        {
            _eventRepository = serviceProvider.GetRequiredService<IEventRepository>();
        }

        public int RunCount => _runCount;
        public bool ProcessFirstEvent { get; set; }

        protected override IList<Type> GetActionTypes() => [];

        public override async Task<ICollection<EventContext>> RunAsync(ICollection<EventContext> contexts)
        {
            if (Interlocked.Increment(ref _runCount) == 1)
            {
                foreach (var context in contexts)
                {
                    if (ProcessFirstEvent && context == contexts.First())
                    {
                        context.IsProcessed = true;
                        context.IsDiscarded = true;
                        await _eventRepository.SaveAsync(context.Event);
                        continue;
                    }

                    context.SetError("Simulated transient pipeline failure.", new InvalidOperationException("Simulated transient pipeline failure."));
                }

                return contexts;
            }

            foreach (var context in contexts)
            {
                context.IsProcessed = true;
                await _eventRepository.SaveAsync(context.Event);
            }

            return contexts;
        }
    }

    private class FailOnceCacheProxy : DispatchProxy
    {
        public ICacheClient Inner { get; set; } = null!;
        public bool FailDiscardedIncrement { get; set; }
        public bool FailReservationAcknowledgement { get; set; }
        public bool FailCompletionNotificationRead { get; set; }
        private bool HasCompletedReservation { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (FailCompletionNotificationRead && HasCompletedReservation && targetMethod.Name == "GetAllAsync" && targetMethod.GetGenericArguments().FirstOrDefault() == typeof(int))
            {
                FailCompletionNotificationRead = false;
                throw new InvalidOperationException("Simulated notification usage read failure after reservation completion.");
            }
            if (FailReservationAcknowledgement && targetMethod.Name == "SetAllAsync" && args?[0] is IDictionary<string, string> values && values.Keys.Any(key => key.Contains("ingest-reservation:", StringComparison.Ordinal)))
            {
                FailReservationAcknowledgement = false;
                throw new InvalidOperationException("Simulated acknowledgement failure after retry enqueue.");
            }
            if (FailDiscardedIncrement && targetMethod.Name == "IncrementAsync" && args?[0] is string key && key.EndsWith(":discarded", StringComparison.Ordinal))
            {
                FailDiscardedIncrement = false;
                throw new InvalidOperationException("Simulated discarded usage write failure after reservation completion.");
            }

            if (targetMethod.Name == "SetAllAsync" && args?[0] is IDictionary<string, string> updates)
                HasCompletedReservation |= updates.Any(update => update.Key.Contains("ingest-reservation:", StringComparison.Ordinal) && JsonNode.Parse(update.Value)?["State"]?.GetValue<int>() == 1);

            return targetMethod.Invoke(Inner, args);
        }
    }

    private class OrganizationRepositoryProxy : DispatchProxy
    {
        public Organization Organization { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            return targetMethod.Name switch
            {
                "GetByIdAsync" => Task.FromResult<Organization?>(Organization.DeepClone()),
                _ => throw new NotSupportedException(targetMethod.Name)
            };
        }
    }

    private class ProjectRepositoryProxy : DispatchProxy
    {
        public Project Project { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            return targetMethod.Name switch
            {
                "GetByIdAsync" => Task.FromResult<Project?>(Project.DeepClone()),
                "GetCountByOrganizationIdAsync" => Task.FromResult(new CountResult(1)),
                _ => throw new NotSupportedException(targetMethod.Name)
            };
        }
    }

    private class EventRepositoryProxy : DispatchProxy
    {
        private readonly List<PersistentEvent> _savedEvents = [];

        public IReadOnlyList<PersistentEvent> SavedEvents => _savedEvents;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == "SaveAsync" && args is { Length: > 0 })
            {
                if (args[0] is PersistentEvent persistentEvent)
                {
                    _savedEvents.Add(persistentEvent);
                    return Task.FromResult(persistentEvent);
                }

                if (args[0] is IEnumerable<PersistentEvent> events)
                {
                    _savedEvents.AddRange(events);
                    return Task.CompletedTask;
                }

                throw new NotSupportedException("SaveAsync argument type");
            }

            if (targetMethod.Name == "CountAsync")
                return Task.FromResult(new CountResult(_savedEvents.Count));

            throw new NotSupportedException(targetMethod.Name);
        }
    }
}
