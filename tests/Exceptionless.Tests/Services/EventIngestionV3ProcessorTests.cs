using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Services;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class EventIngestionV3ProcessorTests
{
    private readonly Organization _organization = new() { Id = "507f191e810c19729de860ea", Name = "Test" };
    private readonly Project _project = new() { Id = "507f191e810c19729de860eb", OrganizationId = "507f191e810c19729de860ea", Name = "Test" };

    [Fact]
    public async Task ProcessAsync_PersistenceFailure_ReleasesReservationWithoutCommit()
    {
        var quota = new RecordingQuotaService { Available = 1 };
        var writer = new RecordingWriter { Exception = new InvalidOperationException("storage unavailable") };
        var processor = CreateProcessor(quota: quota, writer: writer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync([CreateEvent("event-1")], _organization, _project, CancellationToken.None));

        Assert.Equal(1, quota.Reserved);
        Assert.Equal(1, quota.Released);
        Assert.Equal(0, quota.Committed);
    }

    [Fact]
    public async Task ProcessAsync_DiscardedRoute_SkipsQuotaMaterializationAndWrite()
    {
        var quota = new RecordingQuotaService { Available = 1 };
        var materializer = new RecordingMaterializer();
        var writer = new RecordingWriter();
        var routes = new RecordingRouteResolver { DiscardedIds = ["event-1"] };
        var processor = CreateProcessor(quota, materializer, writer, routes);

        EventIngestionV3Response response = await processor.ProcessAsync([CreateEvent("event-1")], _organization, _project, CancellationToken.None);

        Assert.Equal(1, response.Discarded);
        Assert.Equal(0, quota.Reserved);
        Assert.Equal(0, materializer.Count);
        Assert.Empty(writer.Writes);
    }

    [Fact]
    public async Task ProcessAsync_PartialQuota_AdmitsInputOrderAndBlocksRemainder()
    {
        var quota = new RecordingQuotaService { Available = 2 };
        var writer = new RecordingWriter();
        var processor = CreateProcessor(quota: quota, writer: writer);

        EventIngestionV3Response response = await processor.ProcessAsync(
            [CreateEvent("event-1"), CreateEvent("event-2"), CreateEvent("event-3")],
            _organization,
            _project,
            CancellationToken.None);

        Assert.Equal(2, response.Persisted);
        Assert.Equal(1, response.Blocked);
        Assert.Equal(["event-1", "event-2"], writer.Writes.Select(write => write.ClientId));
        Assert.Equal(2, quota.Committed);
        Assert.Equal(2, quota.Released);
    }

    [Fact]
    public async Task ProcessAsync_PreCancelled_StopsBeforePipelineWork()
    {
        var quota = new RecordingQuotaService { Available = 1 };
        var writer = new RecordingWriter();
        var processor = CreateProcessor(quota: quota, writer: writer);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => processor.ProcessAsync([CreateEvent("event-1")], _organization, _project, cancellation.Token));

        Assert.Equal(0, quota.Reserved);
        Assert.Empty(writer.Writes);
    }

    private static EventIngestionV3Event CreateEvent(string id) => new() { Id = id, Type = Event.KnownTypes.Log, Source = id };

    private static EventIngestionV3Processor CreateProcessor(
        RecordingQuotaService? quota = null,
        RecordingMaterializer? materializer = null,
        RecordingWriter? writer = null,
        RecordingRouteResolver? routes = null)
    {
        return new EventIngestionV3Processor(
            new RecordingFingerprintService(),
            routes ?? new RecordingRouteResolver(),
            materializer ?? new RecordingMaterializer(),
            writer ?? new RecordingWriter(),
            quota ?? new RecordingQuotaService { Available = Int32.MaxValue },
            TimeProvider.System);
    }

    private sealed class RecordingFingerprintService : IStackFingerprintService
    {
        public StackFingerprint Create(EventIngestionV3Event source, Organization organization, Project project)
        {
            return new StackFingerprint(source.Id, new Dictionary<string, string> { ["Type"] = source.Type, ["Source"] = source.Source ?? String.Empty });
        }
    }

    private sealed class RecordingRouteResolver : IStackRouteResolver
    {
        public HashSet<string> DiscardedIds { get; init; } = [];

        public Task<IReadOnlyDictionary<string, StackRoute>> ResolveAsync(string projectId, IReadOnlyCollection<string> signatureHashes, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyDictionary<string, StackRoute> routes = signatureHashes
                .Where(DiscardedIds.Contains)
                .ToDictionary(id => id, id => new StackRoute(id.PadRight(24, '0')[..24], StackStatus.Discarded));
            return Task.FromResult(routes);
        }

        public Task UpdateAsync(string projectId, string signatureHash, StackRoute route) => Task.CompletedTask;

        public Task RemoveAsync(string projectId, string signatureHash) => Task.CompletedTask;
    }

    private sealed class RecordingMaterializer : IEventMaterializer
    {
        public int Count { get; private set; }

        public PersistentEvent Materialize(EventIngestionV3Event source, StackFingerprint fingerprint, Organization organization, Project project)
        {
            Count++;
            return new PersistentEvent
            {
                Type = source.Type,
                Source = source.Source,
                Date = DateTimeOffset.UtcNow,
                OrganizationId = organization.Id,
                ProjectId = project.Id
            };
        }
    }

    private sealed class RecordingWriter : IEventBatchWriter
    {
        public Exception? Exception { get; init; }
        public IReadOnlyCollection<EventIngestionWrite> Writes { get; private set; } = [];

        public Task<EventBatchWriteResult> WriteAsync(IReadOnlyCollection<EventIngestionWrite> writes, Organization organization, Project project, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Writes = writes;
            if (Exception is not null)
                throw Exception;
            return Task.FromResult(new EventBatchWriteResult(writes.Count, 0));
        }
    }

    private sealed class RecordingQuotaService : IIngestionQuotaService
    {
        public int Available { get; init; }
        public int Reserved { get; private set; }
        public int Committed { get; private set; }
        public int Released { get; private set; }

        public Task<int> ReserveAsync(string organizationId, int eventCount)
        {
            Reserved = Math.Min(Available, eventCount);
            return Task.FromResult(Reserved);
        }

        public Task CommitAsync(string organizationId, string projectId, int eventCount)
        {
            Committed += eventCount;
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(string organizationId, int eventCount)
        {
            Released += eventCount;
            return Task.CompletedTask;
        }

        public Task TrackBlockedAsync(string organizationId, string projectId, int eventCount) => Task.CompletedTask;
        public Task TrackDiscardedAsync(string organizationId, string projectId, int eventCount) => Task.CompletedTask;
    }
}
