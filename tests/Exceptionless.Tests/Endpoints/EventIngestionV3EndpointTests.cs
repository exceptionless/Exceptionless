using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Utility;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Endpoints;

public sealed class EventIngestionV3EndpointTests : IntegrationTestsBase
{
    private readonly IEventRepository _eventRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;

    public EventIngestionV3EndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        GetService<AppOptions>().EventIngestionV3.Enabled = true;
        _eventRepository = GetService<IEventRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _stackRepository = GetService<IStackRepository>();
        _ = GetService<EventIngestionSideEffectsWorkItemHandler>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task Post_ChunkedTopLevelValues_PersistsEveryEventInline()
    {
        const string payload = """
            {"id":"v3-stream-event-0001","type":"log","source":"Example.Service","message":"first","reference_id":"v3-stream-ref-0001"}
            {"id":"v3-stream-event-0002","type":"log","source":"Example.Service","message":"second","reference_id":"v3-stream-ref-0002"}
            """;

        using var content = new UnknownLengthJsonContent(Encoding.UTF8.GetBytes(payload));
        using HttpResponseMessage httpResponse = await PostAsync(content);
        EventIngestionV3Response response = await DeserializeAsync(httpResponse);

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.Equal(2, response.Received);
        Assert.Equal(2, response.Persisted);
        Assert.Equal(0, response.Discarded);
        await RefreshDataAsync();
        Assert.Single((await _eventRepository.GetByReferenceIdAsync(TestConstants.ProjectId, "v3-stream-ref-0001")).Documents);
        Assert.Single((await _eventRepository.GetByReferenceIdAsync(TestConstants.ProjectId, "v3-stream-ref-0002")).Documents);
    }

    [Fact]
    public async Task Post_GzipError_ParsesStructuredStackOnServer()
    {
        const string payload = """
            {"id":"v3-gzip-error-0001","type":"error","message":"failed","reference_id":"v3-gzip-ref-0001","exception_type":"System.InvalidOperationException","stack_trace":"at Example.OrderService.Save() in /src/OrderService.cs:line 42"}
            """;
        byte[] compressed;
        await using (var output = new MemoryStream())
        {
            await using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
                await gzip.WriteAsync(Encoding.UTF8.GetBytes(payload), TestCancellationToken);
            compressed = output.ToArray();
        }

        using var content = new ByteArrayContent(compressed);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-ndjson");
        content.Headers.ContentEncoding.Add("gzip");
        using HttpResponseMessage httpResponse = await PostAsync(content);
        EventIngestionV3Response response = await DeserializeAsync(httpResponse);

        Assert.Equal(1, response.Persisted);
        await RefreshDataAsync();
        var ev = Assert.Single((await _eventRepository.GetByReferenceIdAsync(TestConstants.ProjectId, "v3-gzip-ref-0001")).Documents);
        Assert.NotNull(ev);
        var error = ev.GetError(GetService<Foundatio.Serializer.ITextSerializer>(), GetService<ILogger<EventIngestionV3EndpointTests>>());
        var frame = Assert.Single(error!.StackTrace!);
        Assert.Equal("Example", frame.DeclaringNamespace);
        Assert.Equal("OrderService", frame.DeclaringType);
        Assert.Equal("Save", frame.Name);
        Assert.Equal(42, frame.LineNumber);
    }

    [Fact]
    public async Task Post_MalformedGzipBody_ReturnsBadRequest()
    {
        using var content = new ByteArrayContent("not-a-gzip-stream"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-ndjson");
        content.Headers.ContentEncoding.Add("gzip");

        using HttpResponseMessage response = await PostAsync(content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_MalformedBrotliBody_ReturnsBadRequest()
    {
        using var content = new ByteArrayContent("not-a-brotli-stream"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-ndjson");
        content.Headers.ContentEncoding.Add("br");

        using HttpResponseMessage response = await PostAsync(content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_ClientMetadata_PersistsFirstClassEventData()
    {
        const string payload = """
            {"id":"v3-client-metadata-0001","type":"log","message":"started","reference_id":"v3-client-metadata-ref-0001","version":"3.4.0","level":"info","client":{"name":"exceptionless.go","version":"1.2.0"}}
            """;

        using HttpResponseMessage httpResponse = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/x-ndjson"));
        EventIngestionV3Response response = await DeserializeAsync(httpResponse);

        Assert.Equal(1, response.Persisted);
        await RefreshDataAsync();
        var ev = Assert.Single((await _eventRepository.GetByReferenceIdAsync(TestConstants.ProjectId, "v3-client-metadata-ref-0001")).Documents);
        Assert.Equal("3.4.0", ev.GetVersion());
        Assert.Equal("info", ev.GetLevel());
        var client = ev.GetSubmissionClient(
            GetService<Foundatio.Serializer.ITextSerializer>(),
            GetService<ILogger<EventIngestionV3EndpointTests>>());
        Assert.NotNull(client);
        Assert.Equal("exceptionless.go", client.UserAgent);
        Assert.Equal("1.2.0", client.Version);
    }

    [Fact]
    public async Task Post_ValuesAtDurableLimits_PersistsWithoutTruncationOrDropping()
    {
        string message = new('m', EventIngestionV3Limits.MaximumMessageLength);
        string referenceId = new('r', EventIngestionV3Limits.MaximumReferenceIdLength);
        string tag = new('t', EventIngestionV3Limits.MaximumTagLength);
        string title = new('s', EventIngestionV3Limits.MaximumStackTitleLength);
        var source = new EventIngestionV3Event
        {
            Id = "v3-durable-boundaries-0001",
            Type = Event.KnownTypes.Log,
            Message = message,
            ReferenceId = referenceId,
            Tags = [tag],
            Stacking = new EventIngestionV3Stacking
            {
                Title = title,
                SignatureData = new Dictionary<string, string> { ["boundary"] = "exact" }
            }
        };
        string payload = JsonSerializer.Serialize(
            source,
            Exceptionless.Core.Serialization.EventIngestionJsonContext.Default.EventIngestionV3Event);

        using HttpResponseMessage httpResponse = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/x-ndjson"));
        EventIngestionV3Response response = await DeserializeAsync(httpResponse);

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.Equal(1, response.Persisted);
        await RefreshDataAsync();
        var ev = Assert.Single((await _eventRepository.GetByReferenceIdAsync(TestConstants.ProjectId, referenceId)).Documents);
        Assert.Equal(message, ev.Message);
        Assert.Equal(tag, Assert.Single(ev.Tags!));
        var stack = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stack);
        Assert.Equal(title, stack.Title);
    }

    [Fact]
    public async Task Post_ReservedTopLevelDataKey_ReturnsValidationProblemWithoutPersistence()
    {
        const string referenceId = "v3-reserved-data-ref-0001";
        const string payload = """
            {"id":"v3-reserved-data-0001","type":"log","reference_id":"v3-reserved-data-ref-0001","data":{"@request":{"cookies":{"authorization":"secret"}}}}
            """;

        using HttpResponseMessage response = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/x-ndjson"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await RefreshDataAsync();
        Assert.Empty((await _eventRepository.GetByReferenceIdAsync(TestConstants.ProjectId, referenceId)).Documents);
    }

    [Fact]
    public async Task Post_NullManualStackingValue_ReturnsValidationProblem()
    {
        const string payload = """
            {"id":"v3-null-manual-stack-0001","type":"log","stacking":{"signature_data":{"hash":null}}}
            """;

        using HttpResponseMessage response = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/x-ndjson"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_ReplayedEvent_ReturnsDuplicateWithoutSecondWrite()
    {
        const string payload = """{"id":"v3-duplicate-event-0001","type":"log","message":"once","reference_id":"v3-duplicate-ref-0001"}""";
        var workItemQueue = GetService<IQueue<WorkItemData>>();
        var initialQueueStats = await workItemQueue.GetQueueStatsAsync();

        using HttpResponseMessage firstHttpResponse = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/x-ndjson"));
        EventIngestionV3Response first = await DeserializeAsync(firstHttpResponse);
        var firstQueueStats = await workItemQueue.GetQueueStatsAsync();
        await RefreshDataAsync();
        using HttpResponseMessage secondHttpResponse = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/x-ndjson"));
        EventIngestionV3Response second = await DeserializeAsync(secondHttpResponse);
        var secondQueueStats = await workItemQueue.GetQueueStatsAsync();

        Assert.Equal(1, first.Persisted);
        Assert.Equal(0, first.Duplicate);
        Assert.Equal(0, second.Persisted);
        Assert.Equal(1, second.Duplicate);
        Assert.Equal(initialQueueStats.Enqueued + 1, firstQueueStats.Enqueued);
        Assert.Equal(firstQueueStats.Enqueued, secondQueueStats.Enqueued);
        Assert.Single((await _eventRepository.GetByReferenceIdAsync(TestConstants.ProjectId, "v3-duplicate-ref-0001")).Documents);
    }

    [Fact]
    public async Task Post_RetryAfterStackOnlyWrite_RecoversFirstOccurrence()
    {
        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        var organization = await GetService<IOrganizationRepository>().GetByIdAsync(project.OrganizationId);
        Assert.NotNull(organization);
        DateTimeOffset eventDate = TimeProvider.GetUtcNow();
        var source = new EventIngestionV3Event
        {
            Id = "v3-first-occurrence-recovery-01",
            Type = Event.KnownTypes.Log,
            Date = eventDate,
            Source = "Example.FirstOccurrence",
            Message = "recover first event",
            ReferenceId = "v3-first-recovery-ref-01"
        };
        StackFingerprint fingerprint = GetService<StackFingerprintService>().Create(source, organization, project);
        string eventId = EventBatchWriter.GetDeterministicEventId(project.Id, source.Id, eventDate.UtcDateTime);
        await _stackRepository.AddAsync(new Stack
        {
            OrganizationId = organization.Id,
            ProjectId = project.Id,
            Type = source.Type,
            Status = StackStatus.Open,
            SignatureHash = fingerprint.SignatureHash,
            SignatureInfo = new SettingsDictionary(fingerprint.SignatureData.ToDictionary(pair => pair.Key, pair => pair.Value)),
            DuplicateSignature = $"{project.Id}:{fingerprint.SignatureHash}",
            Title = source.Message,
            TotalOccurrences = 0,
            FirstOccurrence = TimeProvider.GetUtcNow().UtcDateTime,
            LastOccurrence = TimeProvider.GetUtcNow().UtcDateTime,
            IngestionFirstEventId = eventId
        }, o => o.ImmediateConsistency().Cache());

        string payload = JsonSerializer.Serialize(source, Exceptionless.Core.Serialization.EventIngestionJsonContext.Default.EventIngestionV3Event);
        using HttpResponseMessage httpResponse = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/x-ndjson"));
        EventIngestionV3Response response = await DeserializeAsync(httpResponse);

        Assert.Equal(1, response.Persisted);
        await RefreshDataAsync();
        var persisted = Assert.Single((await _eventRepository.GetByReferenceIdAsync(project.Id, source.ReferenceId)).Documents);
        Assert.Equal(eventId, persisted.Id);
        Assert.True(persisted.IsFirstOccurrence);
    }

    [Fact]
    public async Task Post_DiscardedStack_DoesNotMaterializeOrPersistEvent()
    {
        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        var organization = await GetService<IOrganizationRepository>().GetByIdAsync(project.OrganizationId);
        Assert.NotNull(organization);
        var source = new EventIngestionV3Event
        {
            Id = "v3-discarded-event-01",
            Type = Event.KnownTypes.Error,
            Message = "discard me",
            ExceptionType = "System.InvalidOperationException",
            StackTrace = "at Example.OrderService.Save() in /src/OrderService.cs:line 42",
            ReferenceId = "v3-discard-ref-0001"
        };
        StackFingerprint fingerprint = GetService<StackFingerprintService>().Create(source, organization, project);
        await _stackRepository.AddAsync(new Stack
        {
            OrganizationId = TestConstants.OrganizationId,
            ProjectId = TestConstants.ProjectId,
            Type = Event.KnownTypes.Error,
            Status = StackStatus.Discarded,
            SignatureHash = fingerprint.SignatureHash,
            SignatureInfo = new SettingsDictionary(fingerprint.SignatureData.ToDictionary(pair => pair.Key, pair => pair.Value)),
            DuplicateSignature = $"{TestConstants.ProjectId}:{fingerprint.SignatureHash}",
            Title = "discarded",
            FirstOccurrence = TimeProvider.GetUtcNow().UtcDateTime,
            LastOccurrence = TimeProvider.GetUtcNow().UtcDateTime
        }, o => o.ImmediateConsistency().Cache());

        string payload = JsonSerializer.Serialize(source, Exceptionless.Core.Serialization.EventIngestionJsonContext.Default.EventIngestionV3Event);
        using HttpResponseMessage httpResponse = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/x-ndjson"));
        EventIngestionV3Response response = await DeserializeAsync(httpResponse);

        Assert.Equal(1, response.Discarded);
        Assert.Equal(0, response.Persisted);
        await RefreshDataAsync();
        Assert.Empty((await _eventRepository.GetByReferenceIdAsync(TestConstants.ProjectId, source.ReferenceId)).Documents);
    }

    [Fact]
    public async Task Post_DiscardedStackWithHugeInvalidOptionalContext_DiscardsBeforeFullDeserialization()
    {
        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        var organization = await GetService<IOrganizationRepository>().GetByIdAsync(project.OrganizationId);
        Assert.NotNull(organization);
        var routingEvent = new EventIngestionV3Event
        {
            Id = "v3-discarded-projection-01",
            Type = Event.KnownTypes.Error,
            ExceptionType = "Example.ProjectedException",
            StackTrace = "at Example.Projected.Run() in /src/Projected.cs:line 42"
        };
        StackFingerprint fingerprint = GetService<StackFingerprintService>().Create(routingEvent, organization, project);
        await _stackRepository.AddAsync(new Stack
        {
            OrganizationId = TestConstants.OrganizationId,
            ProjectId = TestConstants.ProjectId,
            Type = Event.KnownTypes.Error,
            Status = StackStatus.Discarded,
            SignatureHash = fingerprint.SignatureHash,
            SignatureInfo = new SettingsDictionary(fingerprint.SignatureData.ToDictionary(pair => pair.Key, pair => pair.Value)),
            DuplicateSignature = $"{TestConstants.ProjectId}:{fingerprint.SignatureHash}",
            Title = "discarded projection",
            FirstOccurrence = TimeProvider.GetUtcNow().UtcDateTime,
            LastOccurrence = TimeProvider.GetUtcNow().UtcDateTime
        }, o => o.ImmediateConsistency().Cache());

        string payload = $$"""
            {"id":"{{routingEvent.Id}}","type":"error","exception_type":"{{routingEvent.ExceptionType}}","stack_trace":"{{routingEvent.StackTrace}}","data":{"value":"{{new string('x', 64 * 1024)}}"},"request":"not-an-object"}
            """;
        using HttpResponseMessage httpResponse = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/x-ndjson"));
        EventIngestionV3Response response = await DeserializeAsync(httpResponse);

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.Equal(1, response.Received);
        Assert.Equal(1, response.Discarded);
        Assert.Equal(0, response.Invalid);
        Assert.Equal(0, response.Persisted);
    }

    [Fact]
    public async Task Post_TopLevelArray_ReturnsProblemDetails()
    {
        using HttpResponseMessage response = await PostAsync(new StringContent("[{\"id\":\"event-1\",\"type\":\"log\"}]", Encoding.UTF8, "application/x-ndjson"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_BrotliBody_PersistsEvent()
    {
        const string payload = """{"id":"v3-brotli-event-0001","type":"log","message":"brotli","reference_id":"v3-brotli-ref-0001"}""";
        byte[] compressed;
        await using (var output = new MemoryStream())
        {
            await using (var brotli = new BrotliStream(output, CompressionMode.Compress, leaveOpen: true))
                await brotli.WriteAsync(Encoding.UTF8.GetBytes(payload), TestCancellationToken);
            compressed = output.ToArray();
        }

        using var content = new ByteArrayContent(compressed);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-ndjson");
        content.Headers.ContentEncoding.Add("br");
        using HttpResponseMessage httpResponse = await PostAsync(content);
        EventIngestionV3Response response = await DeserializeAsync(httpResponse);

        Assert.Equal(1, response.Persisted);
        await RefreshDataAsync();
        Assert.Single((await _eventRepository.GetByReferenceIdAsync(TestConstants.ProjectId, "v3-brotli-ref-0001")).Documents);
    }

    [Fact]
    public async Task Post_UnsupportedContentEncoding_ReturnsUnsupportedMediaType()
    {
        using var content = new StringContent("{\"id\":\"event-1\",\"type\":\"log\"}", Encoding.UTF8, "application/x-ndjson");
        content.Headers.ContentEncoding.Add("deflate");

        using HttpResponseMessage response = await PostAsync(content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Post_EmptyStream_ReturnsEmptySuccess()
    {
        using HttpResponseMessage httpResponse = await PostAsync(new StringContent(String.Empty, Encoding.UTF8, "application/x-ndjson"));
        EventIngestionV3Response response = await DeserializeAsync(httpResponse);

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.Equal(0, response.Received);
        Assert.Equal(0, response.Persisted);
    }

    [Fact]
    public async Task Post_TruncatedJson_ReturnsBadRequest()
    {
        using HttpResponseMessage response = await PostAsync(new StringContent("{\"id\":\"event-1\",\"type\":", Encoding.UTF8, "application/x-ndjson"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_TooManyEvents_ReturnsRequestEntityTooLarge()
    {
        var options = GetService<AppOptions>().EventIngestionV3;
        int originalLimit = options.MaximumEventsPerRequest;
        options.MaximumEventsPerRequest = 1;
        try
        {
            const string payload = """
                {"id":"v3-limit-event-0001","type":"log"}
                {"id":"v3-limit-event-0002","type":"log"}
                """;
            using HttpResponseMessage response = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/x-ndjson"));

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        }
        finally
        {
            options.MaximumEventsPerRequest = originalLimit;
        }
    }

    [Fact]
    public async Task Post_DuplicateIdInSameStream_PersistsAndChargesOnce()
    {
        const string payload = """
            {"id":"v3-stream-duplicate-0001","type":"log","message":"first","reference_id":"v3-stream-duplicate-ref-0001"}
            {"id":"v3-stream-duplicate-0001","type":"log","message":"retry","reference_id":"v3-stream-duplicate-ref-0001"}
            """;

        using HttpResponseMessage httpResponse = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/x-ndjson"));
        EventIngestionV3Response response = await DeserializeAsync(httpResponse);

        Assert.Equal(2, response.Received);
        Assert.Equal(1, response.Persisted);
        Assert.Equal(1, response.Duplicate);
        await RefreshDataAsync();
        Assert.Single((await _eventRepository.GetByReferenceIdAsync(TestConstants.ProjectId, "v3-stream-duplicate-ref-0001")).Documents);
    }

    [Fact]
    public async Task Post_AllInvalidEvents_ReturnsUnprocessableEntity()
    {
        using HttpResponseMessage response = await PostAsync(new StringContent("{\"id\":\"\",\"type\":\"log\"}", Encoding.UTF8, "application/x-ndjson"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_CompressedBodyOverLimit_ReturnsRequestEntityTooLarge()
    {
        var options = GetService<AppOptions>().EventIngestionV3;
        long originalLimit = options.MaximumCompressedBodySize;
        options.MaximumCompressedBodySize = 8;
        try
        {
            using HttpResponseMessage response = await PostAsync(new StringContent("{\"id\":\"v3-compressed-limit\",\"type\":\"log\"}", Encoding.UTF8, "application/x-ndjson"));

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        }
        finally
        {
            options.MaximumCompressedBodySize = originalLimit;
        }
    }

    [Fact]
    public async Task Post_DecompressedBodyOverLimit_ReturnsRequestEntityTooLarge()
    {
        var options = GetService<AppOptions>().EventIngestionV3;
        long originalLimit = options.MaximumDecompressedBodySize;
        options.MaximumDecompressedBodySize = 16;
        try
        {
            using HttpResponseMessage response = await PostAsync(new StringContent("{\"id\":\"v3-decompressed-limit\",\"type\":\"log\"}", Encoding.UTF8, "application/x-ndjson"));

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        }
        finally
        {
            options.MaximumDecompressedBodySize = originalLimit;
        }
    }

    [Fact]
    public async Task Post_HighCompressionRatioWithinIndependentLimits_PersistsEvent()
    {
        var options = GetService<AppOptions>().EventIngestionV3;
        long originalCompressedLimit = options.MaximumCompressedBodySize;
        long originalDecompressedLimit = options.MaximumDecompressedBodySize;
        options.MaximumCompressedBodySize = 1024;
        options.MaximumDecompressedBodySize = 8192;
        try
        {
            string payload = $$"""{"id":"v3-high-ratio-0001","type":"log","reference_id":"v3-high-ratio-ref-0001","message":"{{new string('x', 1900)}}"}""";
            byte[] compressed;
            await using (var output = new MemoryStream())
            {
                await using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
                    await gzip.WriteAsync(Encoding.UTF8.GetBytes(payload), TestCancellationToken);
                compressed = output.ToArray();
            }
            Assert.True(compressed.Length < options.MaximumCompressedBodySize);

            using var content = new ByteArrayContent(compressed);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-ndjson");
            content.Headers.ContentEncoding.Add("gzip");
            using HttpResponseMessage httpResponse = await PostAsync(content);
            EventIngestionV3Response response = await DeserializeAsync(httpResponse);

            Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
            Assert.Equal(1, response.Persisted);
        }
        finally
        {
            options.MaximumCompressedBodySize = originalCompressedLimit;
            options.MaximumDecompressedBodySize = originalDecompressedLimit;
        }
    }

    [Fact]
    public async Task Post_CompressedBodyOverDecompressedLimit_ReturnsRequestEntityTooLarge()
    {
        var options = GetService<AppOptions>().EventIngestionV3;
        long originalCompressedLimit = options.MaximumCompressedBodySize;
        long originalDecompressedLimit = options.MaximumDecompressedBodySize;
        options.MaximumCompressedBodySize = 1024;
        options.MaximumDecompressedBodySize = 128;
        try
        {
            string payload = $$"""{"id":"v3-decompressed-gzip-limit","type":"log","message":"{{new string('x', 512)}}"}""";
            byte[] compressed;
            await using (var output = new MemoryStream())
            {
                await using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
                    await gzip.WriteAsync(Encoding.UTF8.GetBytes(payload), TestCancellationToken);
                compressed = output.ToArray();
            }

            using var content = new ByteArrayContent(compressed);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-ndjson");
            content.Headers.ContentEncoding.Add("gzip");
            using HttpResponseMessage response = await PostAsync(content);

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        }
        finally
        {
            options.MaximumCompressedBodySize = originalCompressedLimit;
            options.MaximumDecompressedBodySize = originalDecompressedLimit;
        }
    }

    [Fact]
    public async Task Post_InvalidJsonAfterPersistedPrefix_ReturnsReplayablePartialResult()
    {
        var options = GetService<AppOptions>().EventIngestionV3;
        int originalMicroBatchSize = options.MicroBatchSize;
        options.MicroBatchSize = 1;
        try
        {
            const string payload = """
                {"id":"v3-partial-prefix-0001","type":"log","reference_id":"v3-partial-prefix-ref-0001"}
                {"id":"broken"
                """;
            using HttpResponseMessage response = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/x-ndjson"));
            string json = await response.Content.ReadAsStringAsync(TestCancellationToken);
            using JsonDocument document = JsonDocument.Parse(json);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            JsonElement partialResult = document.RootElement.GetProperty("partial_result");
            Assert.Equal(1, partialResult.GetProperty("received").GetInt32());
            Assert.Equal(1, partialResult.GetProperty("persisted").GetInt32());
            Assert.Contains("Retry the complete request", document.RootElement.GetProperty("retry_guidance").GetString() ?? String.Empty);

            await RefreshDataAsync();
            Assert.Single((await _eventRepository.GetByReferenceIdAsync(TestConstants.ProjectId, "v3-partial-prefix-ref-0001")).Documents);
        }
        finally
        {
            options.MicroBatchSize = originalMicroBatchSize;
        }
    }

    [Fact]
    public async Task Post_NullNestedHeaderValue_ReturnsValidationProblem()
    {
        const string payload = """{"id":"v3-null-header-0001","type":"log","request":{"headers":{"x-test":null}}}""";

        using HttpResponseMessage response = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/x-ndjson"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_AtQuota_DiscardsKnownStackAndBlocksNewEvent()
    {
        var organizationRepository = GetService<IOrganizationRepository>();
        var organization = await organizationRepository.GetByIdAsync(TestConstants.OrganizationId);
        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(organization);
        Assert.NotNull(project);

        organization.MaxEventsPerMonth = 1;
        organization.GetCurrentUsage(TimeProvider).Total = 1;
        await organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache());
        var cache = GetService<ICacheClient>();
        await cache.RemoveAsync($"usage:limits:{organization.Id}");
        await cache.RemoveByPrefixAsync("usage:total:");

        var discarded = new EventIngestionV3Event
        {
            Id = "v3-at-quota-discarded-0001",
            Type = Event.KnownTypes.Error,
            ExceptionType = "Example.DiscardedException",
            StackTrace = "at Example.Discarded.Run() in /src/Discarded.cs:line 10",
            ReferenceId = "v3-at-quota-discarded-ref-0001"
        };
        StackFingerprint fingerprint = GetService<StackFingerprintService>().Create(discarded, organization, project);
        await _stackRepository.AddAsync(new Stack
        {
            OrganizationId = organization.Id,
            ProjectId = project.Id,
            Type = Event.KnownTypes.Error,
            Status = StackStatus.Discarded,
            SignatureHash = fingerprint.SignatureHash,
            SignatureInfo = new SettingsDictionary(fingerprint.SignatureData),
            DuplicateSignature = $"{project.Id}:{fingerprint.SignatureHash}",
            Title = "discarded at quota",
            FirstOccurrence = TimeProvider.GetUtcNow().UtcDateTime,
            LastOccurrence = TimeProvider.GetUtcNow().UtcDateTime
        }, o => o.ImmediateConsistency().Cache());

        string discardedJson = JsonSerializer.Serialize(discarded, Exceptionless.Core.Serialization.EventIngestionJsonContext.Default.EventIngestionV3Event);
        const string activeJson = """{"id":"v3-at-quota-active-0001","type":"log","source":"new-source","reference_id":"v3-at-quota-active-ref-0001"}""";
        using HttpResponseMessage httpResponse = await PostAsync(new StringContent($"{discardedJson}\n{activeJson}", Encoding.UTF8, "application/x-ndjson"));
        EventIngestionV3Response response = await DeserializeAsync(httpResponse);

        Assert.Equal(2, response.Received);
        Assert.Equal(1, response.Discarded);
        Assert.Equal(1, response.Blocked);
        Assert.Equal(0, response.Persisted);
        await RefreshDataAsync();
        Assert.Empty((await _eventRepository.GetByReferenceIdAsync(project.Id, discarded.ReferenceId)).Documents);
        Assert.Empty((await _eventRepository.GetByReferenceIdAsync(project.Id, "v3-at-quota-active-ref-0001")).Documents);
    }

    [Fact]
    public async Task Post_EventOverUtf8RecordSizeLimit_ReturnsRequestEntityTooLarge()
    {
        var options = GetService<AppOptions>().EventIngestionV3;
        long originalLimit = options.MaximumEventSize;
        options.MaximumEventSize = 60;
        try
        {
            using HttpResponseMessage response = await PostAsync(new StringContent("{\"id\":\"v3-event-size-limit\",\"type\":\"log\",\"message\":\"payload\"}", Encoding.UTF8, "application/x-ndjson"));

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        }
        finally
        {
            options.MaximumEventSize = originalLimit;
        }
    }

    [Fact]
    public async Task Post_WithoutClientAuthorization_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_server.BaseAddress, "/api/v3/events"))
        {
            Content = new StringContent("{\"id\":\"event-1\",\"type\":\"log\"}", Encoding.UTF8, "application/x-ndjson")
        };

        using HttpResponseMessage response = await _server.CreateClient().SendAsync(request, TestCancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_ExplicitProjectMismatch_ReturnsNotFound()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_server.BaseAddress, "/api/v3/projects/507f1f77bcf86cd799439011/events"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestConstants.ApiKey);
        request.Content = new StringContent("{\"id\":\"event-1\",\"type\":\"log\"}", Encoding.UTF8, "application/x-ndjson");

        using HttpResponseMessage response = await _server.CreateClient().SendAsync(request, TestCancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_ExplicitProject_AcquiresActualOrganizationStreamPermit()
    {
        var project = await _projectRepository.GetByIdAsync(SampleDataService.INTERNAL_PROJECT_ID);
        Assert.NotNull(project);
        Assert.NotEqual(TestConstants.OrganizationId, project.OrganizationId);

        var limiter = GetService<EventIngestionV3ConcurrencyLimiter>();
        int permitLimit = GetService<AppOptions>().EventIngestionV3.MaximumActiveStreamsPerOrganization;
        var heldLeases = new List<RateLimitLease>(permitLimit);
        try
        {
            for (int index = 0; index < permitLimit; index++)
            {
                RateLimitLease lease = await limiter.AcquireOrganizationActiveStreamAsync(project.OrganizationId, TestCancellationToken);
                Assert.True(lease.IsAcquired);
                heldLeases.Add(lease);
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(_server.BaseAddress, $"/api/v3/projects/{project.Id}/events"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestConstants.UserApiKey);
            request.Content = new StringContent(
                "{\"id\":\"v3-explicit-project-limit-0001\",\"type\":\"log\"}",
                Encoding.UTF8,
                "application/x-ndjson");

            using HttpResponseMessage response = await _server.CreateClient().SendAsync(request, TestCancellationToken);

            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
        finally
        {
            foreach (RateLimitLease lease in heldLeases)
                lease.Dispose();
        }
    }

    [Fact]
    public async Task Post_UnsupportedMediaType_ReturnsUnsupportedMediaType()
    {
        using HttpResponseMessage response = await PostAsync(new StringContent("{\"id\":\"event-1\",\"type\":\"log\"}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Post_JsonOverDepthLimit_ReturnsBadRequest()
    {
        string nested = String.Concat(Enumerable.Repeat("{\"value\":", EventIngestionV3Limits.MaximumJsonDepth + 1));
        nested += "true" + new string('}', EventIngestionV3Limits.MaximumJsonDepth + 1);
        string payload = $"{{\"id\":\"event-1\",\"type\":\"log\",\"data\":{nested}}}";

        using HttpResponseMessage response = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/x-ndjson"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PostAsync(HttpContent content)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_server.BaseAddress, "/api/v3/events"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestConstants.ApiKey);
        request.Content = content;
        return await _server.CreateClient().SendAsync(request, TestCancellationToken);
    }

    private static async Task<EventIngestionV3Response> DeserializeAsync(HttpResponseMessage response)
    {
        string json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize(json, Exceptionless.Core.Serialization.EventIngestionJsonContext.Default.EventIngestionV3Response)
            ?? throw new InvalidOperationException(json);
    }

    private sealed class UnknownLengthJsonContent : HttpContent
    {
        private readonly byte[] _bytes;

        public UnknownLengthJsonContent(byte[] bytes)
        {
            _bytes = bytes;
            Headers.ContentType = new MediaTypeHeaderValue("application/x-ndjson");
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return stream.WriteAsync(_bytes).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
