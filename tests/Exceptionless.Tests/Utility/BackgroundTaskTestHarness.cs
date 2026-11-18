using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Services.BackgroundTasks;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Resilience;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Tests.Utility;

/// <summary>
/// A <see cref="BackgroundTaskService"/> on in-memory infrastructure for isolated tests: no Elasticsearch, a recording
/// publisher instead of the message bus, and a quick resilience policy for lock retries.
/// </summary>
public sealed class BackgroundTaskTestHarness : IDisposable
{
    public BackgroundTaskTestHarness(TimeProvider timeProvider, ILoggerFactory loggerFactory, BackgroundTaskOptions? options = null, Func<ILockProvider, ILockProvider>? decorateLockProvider = null,
        IQueue<WorkItemData>? queue = null, int lockAttempts = 3, Action<InMemoryCacheClientOptions>? configureCache = null, Func<ICacheClient, ICacheClient>? decorateCache = null)
    {
        TimeProvider = timeProvider;
        var cacheOptions = new InMemoryCacheClientOptions { CloneValues = true, TimeProvider = timeProvider, LoggerFactory = loggerFactory };
        configureCache?.Invoke(cacheOptions);
        Cache = new InMemoryCacheClient(cacheOptions);
        MessageBus = new InMemoryMessageBus(new InMemoryMessageBusOptions { TimeProvider = timeProvider, LoggerFactory = loggerFactory });
        LockProvider = new CacheLockProvider(Cache, MessageBus, timeProvider, loggerFactory);
        if (decorateLockProvider is not null)
            LockProvider = decorateLockProvider(LockProvider);
        Queue = queue ?? new InMemoryQueue<WorkItemData>(new InMemoryQueueOptions<WorkItemData> { TimeProvider = timeProvider, LoggerFactory = loggerFactory });
        Publisher = new RecordingPublisher();

        var policies = new ResiliencePolicyProvider(timeProvider, loggerFactory)
            .WithPolicy<BackgroundTaskService>(b => b.WithMaxAttempts(lockAttempts).WithDelay(TimeSpan.FromMilliseconds(10)));
        var taskCache = decorateCache?.Invoke(Cache) ?? Cache;
        Tasks = new BackgroundTaskService(taskCache, LockProvider, Publisher, Queue, policies, options ?? new BackgroundTaskOptions { LockAcquireTimeout = TimeSpan.FromMilliseconds(100) },
            timeProvider, loggerFactory.CreateLogger<BackgroundTaskService>());
    }

    public TimeProvider TimeProvider { get; }
    public InMemoryCacheClient Cache { get; }
    public InMemoryMessageBus MessageBus { get; }
    public ILockProvider LockProvider { get; }
    public IQueue<WorkItemData> Queue { get; }
    public RecordingPublisher Publisher { get; }
    public BackgroundTaskService Tasks { get; }

    public void Dispose()
    {
        Queue.Dispose();
        MessageBus.Dispose();
        Cache.Dispose();
    }

    public sealed class RecordingPublisher : IMessagePublisher
    {
        private readonly List<object> _messages = [];

        /// <summary>When set, publishing throws, like a message bus that is down.</summary>
        public Exception? Failure { get; set; }

        public IReadOnlyList<BackgroundTaskUpdated> Updates
        {
            get
            {
                lock (_messages)
                    return _messages.OfType<BackgroundTaskUpdated>().ToList();
            }
        }

        public Task PublishAsync(Type messageType, object message, MessageOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
                throw Failure;

            lock (_messages)
                _messages.Add(message);

            return Task.CompletedTask;
        }
    }
}
