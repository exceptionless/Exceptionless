using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Utility;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class EventIngestionV3ProcessorTests
{
    private readonly Organization _organization = new() { Id = "507f191e810c19729de860ea", Name = "Test" };
    private readonly Project _project = new() { Id = "507f191e810c19729de860eb", OrganizationId = "507f191e810c19729de860ea", Name = "Test" };

    [Fact]
    public async Task ProcessAsync_MissingRequiredNestedFields_ReturnsPerEventValidationErrors()
    {
        var processor = CreateProcessor();
        EventIngestionV3Event missingId = CreateEvent("event-0") with { Id = null! };
        EventIngestionV3Event missingClientName = CreateEvent("event-1") with
        {
            Client = new EventIngestionV3Client { Name = null!, Version = "1.0" }
        };
        EventIngestionV3Event missingStackingData = CreateEvent("event-2") with
        {
            Stacking = new EventIngestionV3Stacking { SignatureData = null! }
        };
        EventIngestionV3Event nonObjectData = CreateEvent("event-3") with
        {
            Data = JsonSerializer.Deserialize<JsonElement>("[]")
        };
        EventIngestionV3Event missingClientVersion = CreateEvent("event-4") with
        {
            Client = new EventIngestionV3Client { Name = "exceptionless.test", Version = null! }
        };

        EventIngestionV3Response response = await processor.ProcessAsync(
            [missingId, missingClientName, missingStackingData, nonObjectData, missingClientVersion],
            _organization,
            _project,
            CancellationToken.None);

        Assert.Equal(5, response.Invalid);
        Assert.Equal(5, response.Errors.Count);
        Assert.All(response.Errors, error => Assert.Equal("validation_error", error.Code));
    }

    [Fact]
    public async Task ProcessAsync_ContractViolations_ReturnsValidationErrorsWithoutWriting()
    {
        var writer = new RecordingWriter();
        var processor = CreateProcessor(writer: writer);
        EventIngestionV3Event reservedFirstClassData = CreateEvent("reserved-first-class") with
        {
            Data = JsonSerializer.Deserialize<JsonElement>("""{"@request":{"cookies":{"authorization":"secret"}}}""")
        };
        EventIngestionV3Event reservedLegacyData = CreateEvent("reserved-legacy") with
        {
            Data = JsonSerializer.Deserialize<JsonElement>("""{"haserror":true}""")
        };

        EventIngestionV3Response response = await processor.ProcessAsync(
        [
            reservedFirstClassData,
            reservedLegacyData,
            CreateEvent("message-too-long") with { Message = new string('m', EventIngestionV3Limits.MaximumMessageLength + 1) },
            CreateEvent("tag-too-long") with { Tags = [new string('t', EventIngestionV3Limits.MaximumTagLength + 1)] },
            CreateEvent("title-too-long") with
            {
                Stacking = new EventIngestionV3Stacking
                {
                    Title = new string('s', EventIngestionV3Limits.MaximumStackTitleLength + 1),
                    SignatureData = new Dictionary<string, string> { ["key"] = "value" }
                }
            },
            CreateEvent("invalid-reference") with { ReferenceId = "bad_ref!" },
            CreateEvent("short-reference") with { ReferenceId = "short-1" },
            CreateEvent("long-reference") with { ReferenceId = new string('r', EventIngestionV3Limits.MaximumReferenceIdLength + 1) }
        ], _organization, _project, CancellationToken.None);

        Assert.Equal(8, response.Invalid);
        Assert.Equal(8, response.Errors.Count);
        Assert.Contains(response.Errors, error => error.Message.Contains("reserved top-level key '@request'", StringComparison.Ordinal));
        Assert.Contains(response.Errors, error => error.Message.Contains("reserved top-level key 'haserror'", StringComparison.Ordinal));
        Assert.Contains(response.Errors, error => error.Message.Contains("reference_id must contain between", StringComparison.Ordinal));
        Assert.Equal(0, writer.PrepareCalls);
        Assert.Empty(writer.Writes);
    }

    [Fact]
    public async Task ProcessAsync_PreUnixEpochDate_ReturnsValidationErrorWithoutWriting()
    {
        var writer = new RecordingWriter();
        var processor = CreateProcessor(writer: writer);
        EventIngestionV3Event source = CreateEvent("pre-unix-date") with
        {
            Date = new DateTimeOffset(1969, 12, 31, 23, 59, 59, TimeSpan.Zero)
        };

        EventIngestionV3Response response = await processor.ProcessAsync(
            [source], _organization, _project, CancellationToken.None);

        Assert.Equal(1, response.Invalid);
        Assert.Contains("1970-01-01", Assert.Single(response.Errors).Message, StringComparison.Ordinal);
        Assert.Equal(0, writer.PrepareCalls);
        Assert.Empty(writer.Writes);
    }

    [Fact]
    public async Task ProcessAsync_NullManualStackingValue_RejectsBeforeFingerprinting()
    {
        var fingerprints = new RecordingFingerprintService();
        var processor = CreateProcessor(fingerprints: fingerprints);
        EventIngestionV3Event source = CreateEvent("manual-null") with
        {
            Stacking = new EventIngestionV3Stacking
            {
                SignatureData = new Dictionary<string, string> { ["hash"] = null! }
            }
        };

        EventIngestionV3Response response = await processor.ProcessAsync(
            [source], _organization, _project, CancellationToken.None);

        Assert.Equal(1, response.Invalid);
        Assert.Contains("values cannot be null", Assert.Single(response.Errors).Message, StringComparison.Ordinal);
        Assert.Equal(0, fingerprints.Count);
    }

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
    public async Task ProcessBufferedAsync_DiscardedRoute_DoesNotMaterializeHugeInvalidOptionalContext()
    {
        string payload = $$"""{"id":"event-1","type":"log","source":"event-1","data":{"value":"{{new string('x', 128 * 1024)}}"},"request":"not-an-object"}""";
        using var pool = new TrackingMemoryPool();
        PipeReader reader = PipeReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(payload)));
        EventIngestionV3StreamRecord? streamRecord = await EventIngestionV3StreamReader.ReadAsync(
            reader,
            256 * 1024,
            TestContext.Current.CancellationToken,
            pool);
        Assert.True(streamRecord.HasValue);

        EventIngestionV3BufferedRecord bufferedRecord = streamRecord.Value.BufferedRecord;
        try
        {
            var routes = new RecordingRouteResolver { DiscardedIds = ["event-1"] };
            EventIngestionV3Response response = await CreateProcessor(routes: routes).ProcessBufferedAsync(
                [bufferedRecord],
                _organization,
                _project,
                TestContext.Current.CancellationToken);

            Assert.Equal(1, response.Discarded);
            Assert.Equal(0, response.Invalid);
            Assert.False(bufferedRecord.IsMaterialized);
            Assert.Equal(1, pool.OutstandingRentals);
        }
        finally
        {
            bufferedRecord.Dispose();
            await reader.CompleteAsync();
        }

        Assert.Equal(0, pool.OutstandingRentals);
    }

    [Fact]
    public async Task ProcessBufferedAsync_ManualStackingTitle_PreservesTitleForPersistence()
    {
        const string payload = """
            {"id":"event-1","type":"log","stacking":{"title":"Manual stack title","signature_data":{"hash":"event-1"}}}
            """;
        PipeReader reader = PipeReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(payload)));
        EventIngestionV3StreamRecord? streamRecord = await EventIngestionV3StreamReader.ReadAsync(
            reader,
            1024,
            TestContext.Current.CancellationToken);
        Assert.True(streamRecord.HasValue);

        EventIngestionV3BufferedRecord bufferedRecord = streamRecord.Value.BufferedRecord;
        try
        {
            var writer = new RecordingWriter();
            EventIngestionV3Response response = await CreateProcessor(writer: writer).ProcessBufferedAsync(
                [bufferedRecord],
                _organization,
                _project,
                TestContext.Current.CancellationToken);

            Assert.Equal(1, response.Persisted);
            Assert.True(bufferedRecord.IsMaterialized);
            Assert.Equal("Manual stack title", Assert.Single(writer.Writes).Fingerprint.Title);
        }
        finally
        {
            bufferedRecord.Dispose();
            await reader.CompleteAsync();
        }
    }

    [Fact]
    public async Task ProcessAsync_DiscardedRoute_SkipsOptionalPayloadValidation()
    {
        var routes = new RecordingRouteResolver { DiscardedIds = ["event-1"] };
        var processor = CreateProcessor(routes: routes);
        EventIngestionV3Event source = CreateEvent("event-1") with
        {
            Client = new EventIngestionV3Client { Name = null!, Version = "1.0" },
            Data = JsonSerializer.Deserialize<JsonElement>("[]")
        };

        EventIngestionV3Response response = await processor.ProcessAsync(
            [source],
            _organization,
            _project,
            CancellationToken.None);

        Assert.Equal(1, response.Discarded);
        Assert.Equal(0, response.Invalid);
    }

    [Fact]
    public async Task ProcessAsync_PersistedEventWhoseCurrentRouteIsDiscarded_UsesFreeNoRecoveryPath()
    {
        var quota = new RecordingQuotaService { Available = 1 };
        var writer = new RecordingWriter { PersistedClientIds = ["event-1"] };
        var routes = new RecordingRouteResolver { DiscardedIds = ["event-1"] };
        var processor = CreateProcessor(quota: quota, writer: writer, routes: routes);

        EventIngestionV3Response response = await processor.ProcessAsync(
            [CreateEvent("event-1")], _organization, _project, CancellationToken.None);

        Assert.Equal(1, response.Discarded);
        Assert.Equal(0, response.Duplicate);
        Assert.Equal(0, writer.PrepareCalls);
        Assert.Empty(writer.Reconciliations);
        Assert.Equal(0, quota.Committed);
    }

    [Fact]
    public async Task ProcessAsync_OlderFixedVersion_IsDiscardedOnlyForPremiumOrganization()
    {
        var route = new StackRoute(
            "507f1f77bcf86cd799439010",
            StackStatus.Fixed,
            1,
            "2.0.0",
            DateTime.UtcNow.AddMinutes(-1));
        var routes = new RecordingRouteResolver { Routes = { ["event-1"] = route } };

        _organization.HasPremiumFeatures = false;
        var freeWriter = new RecordingWriter();
        EventIngestionV3Response freeResponse = await CreateProcessor(writer: freeWriter, routes: routes).ProcessAsync(
            [CreateEvent("event-1") with { Version = "1.0.0" }],
            _organization,
            _project,
            CancellationToken.None);

        Assert.Equal(1, freeResponse.Persisted);
        Assert.Equal(0, freeResponse.Discarded);
        Assert.False(Assert.Single(freeWriter.Writes).IsRegressionCandidate);

        _organization.HasPremiumFeatures = true;
        var premiumWriter = new RecordingWriter();
        EventIngestionV3Response premiumResponse = await CreateProcessor(writer: premiumWriter, routes: routes).ProcessAsync(
            [CreateEvent("event-1") with { Version = "1.0.0" }],
            _organization,
            _project,
            CancellationToken.None);

        Assert.Equal(0, premiumResponse.Persisted);
        Assert.Equal(1, premiumResponse.Discarded);
        Assert.Empty(premiumWriter.Writes);
    }

    [Fact]
    public async Task ProcessAsync_FixedStack_SelectsChronologicallyEarliestQualifyingRegression()
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        var writer = new RecordingWriter();
        var routes = new RecordingRouteResolver
        {
            Routes =
            {
                ["shared"] = new StackRoute(
                    "507f1f77bcf86cd799439010",
                    StackStatus.Fixed,
                    1,
                    "1.0.0",
                    utcNow.AddMinutes(-10).UtcDateTime)
            }
        };
        var processor = CreateProcessor(writer: writer, routes: routes);

        await processor.ProcessAsync(
        [
            CreateEvent("later") with
            {
                Version = "1.0.0",
                Date = utcNow.AddMinutes(-1),
                Stacking = new EventIngestionV3Stacking { SignatureData = new() { ["hash"] = "shared" } }
            },
            CreateEvent("earlier") with
            {
                Version = "1.0.0",
                Date = utcNow.AddMinutes(-2),
                Stacking = new EventIngestionV3Stacking { SignatureData = new() { ["hash"] = "shared" } }
            }
        ], _organization, _project, CancellationToken.None);

        EventIngestionWrite regression = Assert.Single(writer.Writes, write => write.IsRegressionCandidate);
        Assert.Equal("earlier", regression.ClientId);
    }

    [Fact]
    public async Task ProcessAsync_PremiumFixedStack_DiscardsLaterOlderVersionAfterQualifyingEvent()
    {
        _organization.HasPremiumFeatures = true;
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        var writer = new RecordingWriter();
        var routes = new RecordingRouteResolver
        {
            Routes =
            {
                ["shared"] = new StackRoute(
                    "507f1f77bcf86cd799439010",
                    StackStatus.Fixed,
                    1,
                    "2.0.0",
                    utcNow.AddMinutes(-10).UtcDateTime)
            }
        };
        var processor = CreateProcessor(writer: writer, routes: routes);

        EventIngestionV3Response response = await processor.ProcessAsync(
        [
            CreateEvent("qualifying") with
            {
                Version = "2.0.0",
                Date = utcNow.AddMinutes(-2),
                Stacking = new EventIngestionV3Stacking { SignatureData = new() { ["hash"] = "shared" } }
            },
            CreateEvent("older-version") with
            {
                Version = "1.0.0",
                Date = utcNow.AddMinutes(-1),
                Stacking = new EventIngestionV3Stacking { SignatureData = new() { ["hash"] = "shared" } }
            }
        ], _organization, _project, CancellationToken.None);

        Assert.Equal(1, response.Discarded);
        EventIngestionWrite write = Assert.Single(writer.Writes);
        Assert.Equal("qualifying", write.ClientId);
        Assert.True(write.IsRegressionCandidate);
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
    public async Task ProcessAsync_DuplicateBeforeNewEvent_DoesNotConsumeQuota()
    {
        var quota = new RecordingQuotaService { Available = 1 };
        var writer = new RecordingWriter { PersistedClientIds = ["event-1"] };
        var processor = CreateProcessor(quota: quota, writer: writer);

        EventIngestionV3Response response = await processor.ProcessAsync(
            [CreateEvent("event-1"), CreateEvent("event-2")],
            _organization,
            _project,
            CancellationToken.None);

        Assert.Equal(1, response.Duplicate);
        Assert.Equal(1, response.Persisted);
        Assert.Equal(0, response.Blocked);
        Assert.Equal(["event-2"], writer.Writes.Select(write => write.ClientId));
        Assert.Equal(1, quota.Reserved);
    }

    [Fact]
    public async Task ProcessAsync_QuotaCommitFailure_RetainsReservationLease()
    {
        var quota = new RecordingQuotaService { Available = 1, CommitException = new InvalidOperationException("cache unavailable") };
        var processor = CreateProcessor(quota: quota);

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(
            [CreateEvent("event-1")], _organization, _project, CancellationToken.None));

        Assert.Equal(1, quota.Reserved);
        Assert.Equal(0, quota.Released);
    }

    [Fact]
    public async Task ProcessAsync_OutboxFailure_SettlesDurableEventsBeforeThrowing()
    {
        DateTime createdUtc = DateTime.UtcNow;
        var quota = new RecordingQuotaService { Available = 1 };
        var writer = new RecordingWriter
        {
            Exception = new EventBatchWriteException(
                new InvalidOperationException("queue unavailable"),
                [new EventUsageSettlement("event-1", createdUtc)])
        };
        var processor = CreateProcessor(quota: quota, writer: writer);

        await Assert.ThrowsAsync<EventBatchWriteException>(() => processor.ProcessAsync(
            [CreateEvent("event-1")], _organization, _project, CancellationToken.None));

        Assert.Equal(1, quota.Committed);
        Assert.Equal(1, quota.Released);
    }

    [Fact]
    public async Task ProcessAsync_PersistedDuplicate_ReconcilesSideEffectsWithoutQuotaAdmission()
    {
        var quota = new RecordingQuotaService { Available = 0 };
        var writer = new RecordingWriter
        {
            PersistedClientIds = ["event-1"]
        };
        var processor = CreateProcessor(quota: quota, writer: writer);

        EventIngestionV3Response response = await processor.ProcessAsync(
            [CreateEvent("event-1")], _organization, _project, CancellationToken.None);

        Assert.Equal(1, response.Duplicate);
        Assert.Equal(0, quota.Reserved);
        Assert.Equal(0, quota.Committed);
        Assert.Single(writer.Reconciliations);
    }

    [Fact]
    public async Task ProcessAsync_AgedFixedOldDuplicate_ReconcilesBeforeUniqueOnlyFilters()
    {
        _organization.HasPremiumFeatures = true;
        var quota = new RecordingQuotaService { Available = 0 };
        var writer = new RecordingWriter { PersistedClientIds = ["event-1"] };
        var routes = new RecordingRouteResolver
        {
            Routes =
            {
                ["event-1"] = new StackRoute(
                    "507f1f77bcf86cd799439010",
                    StackStatus.Fixed,
                    1,
                    "2.0.0",
                    DateTime.UtcNow.AddDays(-10))
            }
        };
        var processor = CreateProcessor(quota: quota, writer: writer, routes: routes);

        EventIngestionV3Response response = await processor.ProcessAsync(
            [CreateEvent("event-1") with { Date = DateTimeOffset.UtcNow.AddDays(-4), Version = "1.0.0" }],
            _organization,
            _project,
            CancellationToken.None);

        Assert.Equal(1, response.Duplicate);
        Assert.Equal(0, response.Discarded);
        Assert.Equal(0, quota.Committed);
        Assert.Single(writer.Reconciliations);
    }

    [Fact]
    public async Task ProcessAsync_PersistedDuplicateOutsideRecoveryWindow_DoesNotRerunSideEffectsOrUsage()
    {
        var quota = new RecordingQuotaService { Available = 0 };
        var writer = new RecordingWriter
        {
            PersistedClientIds = ["event-1"],
            RecoveryIneligibleClientIds = ["event-1"]
        };
        var processor = CreateProcessor(quota: quota, writer: writer);

        EventIngestionV3Response response = await processor.ProcessAsync(
            [CreateEvent("event-1")], _organization, _project, CancellationToken.None);

        Assert.Equal(1, response.Duplicate);
        Assert.Equal(0, quota.Committed);
        Assert.Empty(writer.Reconciliations);
    }

    [Fact]
    public async Task ProcessAsync_PersistedDuplicateDoesNotConsumeRegressionCandidateFromNewEvent()
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        var writer = new RecordingWriter { PersistedClientIds = ["duplicate"] };
        var routes = new RecordingRouteResolver
        {
            Routes =
            {
                ["shared"] = new StackRoute(
                    "507f1f77bcf86cd799439010",
                    StackStatus.Fixed,
                    1,
                    "1.0.0",
                    utcNow.AddMinutes(-10).UtcDateTime)
            }
        };
        var processor = CreateProcessor(writer: writer, routes: routes);

        EventIngestionV3Response response = await processor.ProcessAsync(
        [
            CreateEvent("duplicate") with
            {
                Version = "1.0.0",
                Date = utcNow.AddMinutes(-2),
                Stacking = new EventIngestionV3Stacking { SignatureData = new() { ["hash"] = "shared" } }
            },
            CreateEvent("new") with
            {
                Version = "1.0.0",
                Date = utcNow.AddMinutes(-1),
                Stacking = new EventIngestionV3Stacking { SignatureData = new() { ["hash"] = "shared" } }
            }
        ], _organization, _project, CancellationToken.None);

        Assert.Equal(1, response.Duplicate);
        EventIngestionWrite write = Assert.Single(writer.Writes);
        Assert.Equal("new", write.ClientId);
        Assert.True(write.IsRegressionCandidate);
    }

    [Fact]
    public async Task ProcessAsync_PersistedDuplicate_ReconcilesUsingPersistedStackIdentity()
    {
        const string persistedStackId = "507f1f77bcf86cd799439099";
        var writer = new RecordingWriter
        {
            PersistedClientIds = ["event-1"],
            PersistedStackIds = { ["event-1"] = persistedStackId }
        };
        var routes = new RecordingRouteResolver
        {
            Routes =
            {
                ["event-1"] = new StackRoute(
                    "507f1f77bcf86cd799439010",
                    StackStatus.Fixed,
                    1,
                    "1.0.0",
                    DateTime.UtcNow.AddMinutes(-1))
            }
        };
        var processor = CreateProcessor(writer: writer, routes: routes);

        await processor.ProcessAsync(
            [CreateEvent("event-1") with { Version = "1.0.0" }],
            _organization,
            _project,
            CancellationToken.None);

        EventIngestionReconciliation reconciliation = Assert.Single(writer.Reconciliations);
        Assert.Equal(persistedStackId, reconciliation.StackId);
        Assert.NotEqual(routes.Routes["event-1"].StackId, reconciliation.StackId);
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
        RecordingRouteResolver? routes = null,
        RecordingFingerprintService? fingerprints = null)
    {
        return new EventIngestionV3Processor(
            fingerprints ?? new RecordingFingerprintService(),
            routes ?? new RecordingRouteResolver(),
            materializer ?? new RecordingMaterializer(),
            writer ?? new RecordingWriter(),
            quota ?? new RecordingQuotaService { Available = Int32.MaxValue },
            new SemanticVersionParser(NullLoggerFactory.Instance),
            TimeProvider.System);
    }

    private sealed class RecordingFingerprintService : IStackFingerprintService
    {
        public int Count { get; private set; }

        public StackFingerprint Create(EventIngestionV3Event source, Organization organization, Project project)
        {
            Count++;
            string signatureHash = source.Stacking?.SignatureData.TryGetValue("hash", out string? hash) is true ? hash : source.Id;
            return new StackFingerprint(signatureHash, new Dictionary<string, string> { ["Type"] = source.Type, ["Source"] = source.Source ?? String.Empty });
        }
    }

    private sealed class RecordingRouteResolver : IStackRouteResolver
    {
        public HashSet<string> DiscardedIds { get; init; } = [];
        public Dictionary<string, StackRoute> Routes { get; init; } = [];

        public Task<IReadOnlyDictionary<string, StackRoute>> ResolveAsync(string projectId, IReadOnlyCollection<string> signatureHashes, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var routes = new Dictionary<string, StackRoute>(Routes, StringComparer.Ordinal);
            foreach (string id in signatureHashes.Where(DiscardedIds.Contains))
                routes[id] = new StackRoute(id.PadRight(24, '0')[..24], StackStatus.Discarded, 1);
            return Task.FromResult<IReadOnlyDictionary<string, StackRoute>>(routes);
        }

        public Task UpdateAsync(string projectId, string signatureHash, StackRoute route) => Task.CompletedTask;

        public Task RemoveAsync(string projectId, string signatureHash) => Task.CompletedTask;

        public Task<bool> TryMarkRegressedAsync(StackRoute route, string eventId, CancellationToken cancellationToken = default) => Task.FromResult(true);
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
        public HashSet<string> PersistedClientIds { get; init; } = [];
        public Dictionary<string, string> PersistedStackIds { get; init; } = [];
        public Dictionary<string, StackStatus> PersistedStackStatuses { get; init; } = [];
        public HashSet<string> RecoveryIneligibleClientIds { get; init; } = [];
        public int PrepareCalls { get; private set; }
        public IReadOnlyCollection<EventIngestionWrite> Writes { get; private set; } = [];
        public IReadOnlyCollection<EventIngestionReconciliation> Reconciliations { get; private set; } = [];

        public Task<IReadOnlyList<EventIngestionIdentity>> PrepareAsync(
            IReadOnlyCollection<EventIngestionV3Event> events,
            string projectId,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            PrepareCalls++;
            IReadOnlyList<EventIngestionIdentity> identities = events.Select(source =>
            {
                bool persisted = PersistedClientIds.Contains(source.Id);
                string persistedStackId = PersistedStackIds.TryGetValue(source.Id, out string? stackId)
                    ? stackId
                    : "507f1f77bcf86cd799439010";
                return new EventIngestionIdentity(
                    source.Id,
                    source.Id.PadRight(24, '0')[..24],
                    persisted,
                    persisted,
                    persisted ? persistedStackId : null,
                    persisted
                        ? PersistedStackStatuses.GetValueOrDefault(source.Id, StackStatus.Open)
                        : null,
                    !RecoveryIneligibleClientIds.Contains(source.Id),
                    utcNow,
                    source.Date ?? new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)));
            }).ToArray();
            return Task.FromResult(identities);
        }

        public Task ReconcileAsync(
            IReadOnlyCollection<EventIngestionReconciliation> reconciliations,
            Organization organization,
            Project project,
            CancellationToken cancellationToken)
        {
            Reconciliations = reconciliations;
            return Task.CompletedTask;
        }

        public Task<EventBatchWriteResult> WriteAsync(IReadOnlyCollection<EventIngestionWrite> writes, Organization organization, Project project, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Writes = writes;
            if (Exception is not null)
                throw Exception;
            return Task.FromResult(new EventBatchWriteResult(
                writes.Count,
                0,
                writes.Select(write => new EventUsageSettlement(write.Event.Id, write.Event.CreatedUtc)).ToArray()));
        }
    }

    private sealed class RecordingQuotaService : IIngestionQuotaService
    {
        public int Available { get; init; }
        public int Reserved { get; private set; }
        public int Committed { get; private set; }
        public int Released { get; private set; }
        public Exception? CommitException { get; init; }

        public Task<EventIngestionReservation> ReserveAsync(string organizationId, int eventCount)
        {
            Reserved = Math.Min(Available, eventCount);
            return Task.FromResult(new EventIngestionReservation("reservation", organizationId, Reserved));
        }

        public Task CommitAsync(string organizationId, string projectId, IReadOnlyCollection<EventUsageSettlement> settlements)
        {
            if (CommitException is not null)
                throw CommitException;
            Committed += settlements.Count;
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(EventIngestionReservation reservation)
        {
            Released += reservation.Count;
            return Task.CompletedTask;
        }

        public Task TrackBlockedAsync(string organizationId, string projectId, int eventCount) => Task.CompletedTask;
        public Task TrackDiscardedAsync(string organizationId, string projectId, int eventCount) => Task.CompletedTask;
    }
}
