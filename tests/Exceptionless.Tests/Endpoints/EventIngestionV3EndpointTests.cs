using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Utility;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Endpoints;

public sealed class EventIngestionV3EndpointTests : IntegrationTestsBase
{
    private readonly IEventRepository _eventRepository;
    private readonly IStackRepository _stackRepository;
    private readonly JsonSerializerOptions _jsonOptions;

    public EventIngestionV3EndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        GetService<AppOptions>().EventIngestionV3.Enabled = true;
        _eventRepository = GetService<IEventRepository>();
        _stackRepository = GetService<IStackRepository>();
        _jsonOptions = GetService<JsonSerializerOptions>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task Post_MinimalJsonObject_PersistsLogEvent()
    {
        // Act
        using var httpResponse = await PostAsync(new StringContent("""{"message":"hello v3","reference_id":"v3-minimal-0001"}""", Encoding.UTF8, "application/json"));
        var response = await DeserializeAsync(httpResponse);

        // Assert
        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.Equal(1, response.Received);
        Assert.Equal(1, response.Persisted);

        var ev = await GetEventByReferenceIdAsync("v3-minimal-0001");
        Assert.Equal(Event.KnownTypes.Log, ev.Type);
        Assert.Equal("hello v3", ev.Message);
        Assert.NotNull(ev.StackId);
    }

    [Fact]
    public async Task Post_PrettyPrintedObjectWithoutContentType_PersistsEvent()
    {
        // Arrange
        const string payload = """
            {
              "message": "pretty",
              "reference_id": "v3-pretty-0001"
            }
            """;
        var content = new StringContent(payload, Encoding.UTF8);
        content.Headers.ContentType = null;

        // Act
        using var httpResponse = await PostAsync(content);
        var response = await DeserializeAsync(httpResponse);

        // Assert
        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.Equal(1, response.Persisted);
        Assert.Equal("pretty", (await GetEventByReferenceIdAsync("v3-pretty-0001")).Message);
    }

    [Fact]
    public async Task Post_JsonArray_PersistsEachEvent()
    {
        // Arrange
        const string payload = """[{"message":"first","reference_id":"v3-array-0001"},{"message":"second","reference_id":"v3-array-0002"}]""";

        // Act
        using var httpResponse = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/json"));
        var response = await DeserializeAsync(httpResponse);

        // Assert
        Assert.Equal(2, response.Persisted);
        Assert.Equal("first", (await GetEventByReferenceIdAsync("v3-array-0001")).Message);
        Assert.Equal("second", (await GetEventByReferenceIdAsync("v3-array-0002")).Message);
    }

    [Fact]
    public async Task Post_NewlineDelimitedStreamOfUnknownLength_PersistsEveryEventAcrossMicrobatches()
    {
        // Arrange
        var options = GetService<AppOptions>().EventIngestionV3;
        int originalMicroBatchSize = options.MicroBatchSize;
        options.MicroBatchSize = 2;
        string payload = String.Join('\n', Enumerable.Range(1, 5).Select(i => $$"""{"message":"stream {{i}}","reference_id":"v3-stream-000{{i}}"}"""));

        try
        {
            // Act
            using var httpResponse = await PostAsync(new UnknownLengthContent(Encoding.UTF8.GetBytes(payload), "application/x-ndjson"));
            var response = await DeserializeAsync(httpResponse);

            // Assert
            Assert.Equal(5, response.Received);
            Assert.Equal(5, response.Persisted);
            Assert.Equal("stream 5", (await GetEventByReferenceIdAsync("v3-stream-0005")).Message);
        }
        finally
        {
            options.MicroBatchSize = originalMicroBatchSize;
        }
    }

    [Fact]
    public async Task Post_InvalidEventInStream_ReportsItsIndexAndPersistsTheRest()
    {
        // Arrange
        const string payload = """
            {"message":"first","reference_id":"v3-invalid-0001"}
            {"message": broken}
            {"id":5,"message":"wrong id type"}
            {"message":"last","reference_id":"v3-invalid-0004"}
            """;

        // Act
        using var httpResponse = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/x-ndjson"));
        var response = await DeserializeAsync(httpResponse);

        // Assert
        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.Equal(4, response.Received);
        Assert.Equal(2, response.Persisted);
        Assert.Equal(2, response.Invalid);
        Assert.Equal([1, 2], response.Errors.Select(e => e.Index));
        Assert.Equal(EventIngestionV3ErrorCodes.InvalidJson, response.Errors[0].Code);
        Assert.Equal(EventIngestionV3ErrorCodes.InvalidEvent, response.Errors[1].Code);
        Assert.Equal("last", (await GetEventByReferenceIdAsync("v3-invalid-0004")).Message);
    }

    [Fact]
    public async Task Post_OversizedFields_AreTruncatedInsteadOfRejected()
    {
        // Arrange
        string payload = JsonSerializer.Serialize(new
        {
            message = new string('m', 3000),
            reference_id = "v3-truncate-0001",
            tags = Enumerable.Range(0, 60).Select(i => $"tag-{i}").ToArray()
        });

        // Act
        using var httpResponse = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/json"));
        var response = await DeserializeAsync(httpResponse);

        // Assert
        Assert.Equal(1, response.Persisted);
        var ev = await GetEventByReferenceIdAsync("v3-truncate-0001");
        Assert.Equal(2000, ev.Message?.Length);
        Assert.True(ev.Tags?.Count < 60);
    }

    [Fact]
    public async Task Post_FirstClassProperties_AreStoredAsVersionTwoEventData()
    {
        // Arrange
        const string payload = """
            {"type":"log","message":"started","reference_id":"v3-data-0001","version":"3.4.0","level":"info","user":{"identity":"user@example.com","name":"Example User"},"request":{"http_method":"POST","path":"/orders"},"environment":{"machine_name":"web-1"},"data":{"order_id":12}}
            """;

        // Act
        using var httpResponse = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/json"));
        var response = await DeserializeAsync(httpResponse);

        // Assert
        Assert.Equal(1, response.Persisted);
        var ev = await GetEventByReferenceIdAsync("v3-data-0001");
        var serializer = GetService<ITextSerializer>();
        Assert.Equal("3.4.0", ev.GetVersion());
        Assert.Equal("info", ev.GetLevel());
        Assert.Equal("user@example.com", ev.GetUserIdentity(serializer, _logger)?.Identity);
        Assert.Equal("/orders", ev.GetRequestInfo(serializer, _logger)?.Path);
        Assert.Equal("web-1", ev.GetEnvironmentInfo(serializer, _logger)?.MachineName);
        Assert.True(ev.Data?.ContainsKey("order_id"));
    }

    [Fact]
    public async Task Post_StructuredError_UsesTheSameStackAsVersionTwo()
    {
        // Arrange
        var error = new Error
        {
            Message = "Order failed",
            Type = "System.InvalidOperationException",
            StackTrace =
            [
                new StackFrame { DeclaringNamespace = "Example.Orders", DeclaringType = "OrderService", Name = "Place", LineNumber = 42 }
            ]
        };

        // Act
        var versionTwoEvent = await PostVersionTwoEventAsync(new Event
        {
            Type = Event.KnownTypes.Error,
            Message = "Order failed",
            ReferenceId = "v2-parity-error-0001",
            Data = new DataDictionary { { Event.KnownDataKeys.Error, error } }
        });

        string payload = JsonSerializer.Serialize(new { message = "Order failed", reference_id = "v3-parity-error-0001", error }, _jsonOptions);
        using var httpResponse = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/json"));
        var response = await DeserializeAsync(httpResponse);

        // Assert
        Assert.Equal(1, response.Persisted);
        var versionThreeEvent = await GetEventByReferenceIdAsync("v3-parity-error-0001");
        Assert.Equal(Event.KnownTypes.Error, versionThreeEvent.Type);
        Assert.Equal(versionTwoEvent.StackId, versionThreeEvent.StackId);
    }

    [Fact]
    public async Task Post_RawStackTrace_UsesTheSameStackAsVersionTwoSimpleError()
    {
        // Arrange
        const string stackTrace = "   at Example.Orders.OrderService.Place() in /src/OrderService.cs:line 42";

        // Act
        var versionTwoEvent = await PostVersionTwoEventAsync(new Event
        {
            Type = Event.KnownTypes.Error,
            Message = "Order failed",
            ReferenceId = "v2-parity-simple-0001",
            Data = new DataDictionary
            {
                { Event.KnownDataKeys.SimpleError, new SimpleError { Message = "Order failed", Type = "System.InvalidOperationException", StackTrace = stackTrace } }
            }
        });

        string payload = JsonSerializer.Serialize(new
        {
            message = "Order failed",
            reference_id = "v3-parity-simple-0001",
            exception_type = "System.InvalidOperationException",
            stack_trace = stackTrace
        });
        using var httpResponse = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/json"));
        var response = await DeserializeAsync(httpResponse);

        // Assert
        Assert.Equal(1, response.Persisted);
        var versionThreeEvent = await GetEventByReferenceIdAsync("v3-parity-simple-0001");
        Assert.Equal(Event.KnownTypes.Error, versionThreeEvent.Type);
        Assert.Equal(versionTwoEvent.StackId, versionThreeEvent.StackId);
    }

    [Fact]
    public async Task Post_ResentEventId_IsAcknowledgedAsDuplicate()
    {
        // Arrange
        const string payload = """{"id":"0f0b9a8e-5d4c-4b7a-9a43-3f0f2c1d1e11","message":"once","reference_id":"v3-replay-0001"}""";

        // Act
        using var firstResponse = await PostAsync(new StringContent(payload, Encoding.UTF8, "application/json"));
        var first = await DeserializeAsync(firstResponse);
        using var secondResponse = await PostAsync(new StringContent($"{payload}\n{payload}", Encoding.UTF8, "application/x-ndjson"));
        var second = await DeserializeAsync(secondResponse);

        // Assert
        Assert.Equal(1, first.Persisted);
        Assert.Equal(0, second.Persisted);
        Assert.Equal(2, second.Duplicate);
        await RefreshDataAsync();
        Assert.Single((await _eventRepository.GetByReferenceIdAsync(TestConstants.ProjectId, "v3-replay-0001")).Documents);
    }

    [Fact]
    public async Task Post_ResendWhileOriginalIsInProgress_IsRetryableInsteadOfDuplicate()
    {
        // Arrange
        const string id = "1b7c2a90-4f0e-4d8f-8f5e-6c0d9e3b2a10";
        await GetService<ICacheClient>().AddAsync($"ingestion:v3:id:{TestConstants.ProjectId}:{id.ToSHA256()}", "pending", TimeSpan.FromMinutes(1));

        // Act
        using var httpResponse = await PostAsync(new StringContent($$"""{"id":"{{id}}","message":"in progress"}""", Encoding.UTF8, "application/json"));
        using var problem = JsonDocument.Parse(await httpResponse.Content.ReadAsStringAsync(TestCancellationToken));

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, httpResponse.StatusCode);
        Assert.NotNull(httpResponse.Headers.RetryAfter);
        var partialResult = problem.RootElement.GetProperty("partial_result");
        Assert.Equal(1, partialResult.GetProperty("failed").GetInt32());
        Assert.Equal(0, partialResult.GetProperty("duplicate").GetInt32());
        Assert.Equal(EventIngestionV3ErrorCodes.EventInProgress, partialResult.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Post_NonObjectValueForMappedDataKey_IsStoredUnderEscapedKey()
    {
        // Act
        using var httpResponse = await PostAsync(new StringContent("""{"message":"escaped","reference_id":"v3-escaped-0001","data":{"@error":"boom"}}""", Encoding.UTF8, "application/json"));
        var response = await DeserializeAsync(httpResponse);

        // Assert
        Assert.Equal(1, response.Persisted);
        var ev = await GetEventByReferenceIdAsync("v3-escaped-0001");
        Assert.False(ev.Data?.ContainsKey(Event.KnownDataKeys.Error));
        Assert.Equal("boom", ev.Data?["_@error"]);
    }

    [Fact]
    public async Task Post_EventForDiscardedStack_IsDiscarded()
    {
        // Arrange
        using (var firstResponse = await PostAsync(new StringContent("""{"message":"discard me","reference_id":"v3-discard-0001"}""", Encoding.UTF8, "application/json")))
        {
            Assert.Equal(1, (await DeserializeAsync(firstResponse)).Persisted);
        }

        var stack = await _stackRepository.GetByIdAsync((await GetEventByReferenceIdAsync("v3-discard-0001")).StackId);
        Assert.NotNull(stack);
        stack.Status = StackStatus.Discarded;
        await _stackRepository.SaveAsync(stack, o => o.ImmediateConsistency().Cache());

        // Act
        using var httpResponse = await PostAsync(new StringContent("""{"message":"discard me","reference_id":"v3-discard-0002"}""", Encoding.UTF8, "application/json"));
        var response = await DeserializeAsync(httpResponse);

        // Assert
        Assert.Equal(1, response.Discarded);
        Assert.Equal(0, response.Persisted);
        await RefreshDataAsync();
        Assert.Empty((await _eventRepository.GetByReferenceIdAsync(TestConstants.ProjectId, "v3-discard-0002")).Documents);
    }

    [Fact]
    public async Task Post_OrganizationAtEventLimit_ReturnsPaymentRequired()
    {
        // Arrange
        var organizationRepository = GetService<IOrganizationRepository>();
        var organization = await organizationRepository.GetByIdAsync(TestConstants.OrganizationId);
        Assert.NotNull(organization);
        organization.MaxEventsPerMonth = 1;
        organization.GetCurrentUsage(TimeProvider).Total = 1;
        await organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
        await GetService<ICacheClient>().RemoveAllAsync();

        // Act
        using var httpResponse = await PostAsync(new StringContent("""{"message":"blocked"}""", Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.PaymentRequired, httpResponse.StatusCode);
    }

    [Fact]
    public async Task Post_GzipBody_PersistsEvent()
    {
        // Arrange
        byte[] compressed;
        await using (var output = new MemoryStream())
        {
            await using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
            {
                await gzip.WriteAsync("""{"message":"compressed","reference_id":"v3-gzip-0001"}"""u8.ToArray(), TestCancellationToken);
            }

            compressed = output.ToArray();
        }

        var content = new ByteArrayContent(compressed);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Headers.ContentEncoding.Add("gzip");

        // Act
        using var httpResponse = await PostAsync(content);
        var response = await DeserializeAsync(httpResponse);

        // Assert
        Assert.Equal(1, response.Persisted);
        Assert.Equal("compressed", (await GetEventByReferenceIdAsync("v3-gzip-0001")).Message);
    }

    [Fact]
    public async Task Post_MalformedGzipBody_ReturnsBadRequest()
    {
        // Arrange
        var content = new ByteArrayContent("not-a-gzip-stream"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Headers.ContentEncoding.Add("gzip");

        // Act
        using var response = await PostAsync(content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_DecompressedBodyOverLimit_ReturnsRequestEntityTooLarge()
    {
        // Arrange
        var options = GetService<AppOptions>().EventIngestionV3;
        long originalLimit = options.MaximumDecompressedBodySize;
        options.MaximumDecompressedBodySize = 16;

        try
        {
            // Act
            using var response = await PostAsync(new StringContent("""{"message":"too large for the body limit"}""", Encoding.UTF8, "application/json"));

            // Assert
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        }
        finally
        {
            options.MaximumDecompressedBodySize = originalLimit;
        }
    }

    [Fact]
    public async Task Post_MalformedArray_ReturnsBadRequestWithPartialResult()
    {
        // Arrange
        var options = GetService<AppOptions>().EventIngestionV3;
        int originalMicroBatchSize = options.MicroBatchSize;
        options.MicroBatchSize = 1;

        try
        {
            // Act
            using var response = await PostAsync(new StringContent("""[{"message":"kept","reference_id":"v3-partial-0001"} {"message":"no comma"}]""", Encoding.UTF8, "application/json"));
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestCancellationToken));

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(1, problem.RootElement.GetProperty("partial_result").GetProperty("persisted").GetInt32());
            Assert.Equal("kept", (await GetEventByReferenceIdAsync("v3-partial-0001")).Message);
        }
        finally
        {
            options.MicroBatchSize = originalMicroBatchSize;
        }
    }

    [Fact]
    public async Task Post_OnlyInvalidEvents_ReturnsUnprocessableEntity()
    {
        // Act
        using var response = await PostAsync(new StringContent("\"not an event\"\n42", Encoding.UTF8, "application/x-ndjson"));

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Post_EmptyBody_ReturnsEmptyResult()
    {
        // Act
        using var httpResponse = await PostAsync(new StringContent("", Encoding.UTF8, "application/json"));
        var response = await DeserializeAsync(httpResponse);

        // Assert
        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.Equal(0, response.Received);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/xml")]
    [InlineData("application/json; charset=utf-16")]
    public async Task Post_UnsupportedContentType_ReturnsUnsupportedMediaType(string contentType)
    {
        // Arrange
        var content = new ByteArrayContent("""{"message":"hello"}"""u8.ToArray());
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        // Act
        using var response = await PostAsync(content);

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Post_WhenDisabled_ReturnsNotFound()
    {
        // Arrange
        var options = GetService<AppOptions>().EventIngestionV3;
        options.Enabled = false;

        try
        {
            // Act
            using var response = await PostAsync(new StringContent("""{"message":"hello"}""", Encoding.UTF8, "application/json"));

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            options.Enabled = true;
        }
    }

    [Fact]
    public async Task Post_WithoutAuthorization_ReturnsUnauthorized()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_server.BaseAddress, "/api/v3/events"))
        {
            Content = new StringContent("""{"message":"hello"}""", Encoding.UTF8, "application/json")
        };

        // Act
        using var response = await _server.CreateClient().SendAsync(request, TestCancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_ExplicitProjectMismatch_ReturnsNotFound()
    {
        // Act
        using var response = await PostAsync(new StringContent("""{"message":"hello"}""", Encoding.UTF8, "application/json"), "/api/v3/projects/507f1f77bcf86cd799439011/events");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_ExplicitProject_PersistsEvent()
    {
        // Act
        using var httpResponse = await PostAsync(new StringContent("""{"message":"explicit","reference_id":"v3-explicit-0001"}""", Encoding.UTF8, "application/json"), $"/api/v3/projects/{TestConstants.ProjectId}/events");
        var response = await DeserializeAsync(httpResponse);

        // Assert
        Assert.Equal(1, response.Persisted);
        Assert.Equal("explicit", (await GetEventByReferenceIdAsync("v3-explicit-0001")).Message);
    }

    [Fact]
    public async Task Post_OrganizationStreamCapacityBusy_ReturnsTooManyRequestsWithRetryAfter()
    {
        // Arrange
        var limiter = GetService<EventIngestionV3ConcurrencyLimiter>();
        int permitLimit = GetService<AppOptions>().EventIngestionV3.MaximumActiveStreamsPerOrganization;
        var heldLeases = new List<RateLimitLease>(permitLimit);

        try
        {
            for (int index = 0; index < permitLimit; index++)
            {
                var lease = await limiter.AcquireOrganizationActiveStreamAsync(TestConstants.OrganizationId, TestCancellationToken);
                Assert.True(lease.IsAcquired);
                heldLeases.Add(lease);
            }

            // Act
            using var response = await PostAsync(new StringContent("""{"message":"busy"}""", Encoding.UTF8, "application/json"));

            // Assert
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.NotNull(response.Headers.RetryAfter);
        }
        finally
        {
            foreach (var lease in heldLeases)
            {
                lease.Dispose();
            }
        }
    }

    private async Task<PersistentEvent> PostVersionTwoEventAsync(Event ev)
    {
        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events")
            .Content(ev)
            .StatusCodeShouldBeAccepted());

        await GetService<EventPostsJob>().RunAsync(TestCancellationToken);
        return await GetEventByReferenceIdAsync(ev.ReferenceId!);
    }

    private async Task<PersistentEvent> GetEventByReferenceIdAsync(string referenceId)
    {
        await RefreshDataAsync();
        var results = await _eventRepository.GetByReferenceIdAsync(TestConstants.ProjectId, referenceId);
        return Assert.Single(results.Documents);
    }

    private async Task<HttpResponseMessage> PostAsync(HttpContent content, string path = "/api/v3/events")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_server.BaseAddress, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestConstants.ApiKey);
        request.Content = content;
        return await _server.CreateClient().SendAsync(request, TestCancellationToken);
    }

    private async Task<EventIngestionV3Response> DeserializeAsync(HttpResponseMessage response)
    {
        string json = await response.Content.ReadAsStringAsync(TestCancellationToken);
        Assert.True(response.IsSuccessStatusCode, json);
        return JsonSerializer.Deserialize<EventIngestionV3Response>(json, _jsonOptions) ?? throw new InvalidOperationException(json);
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _bytes;

        public UnknownLengthContent(byte[] bytes, string mediaType)
        {
            _bytes = bytes;
            Headers.ContentType = new MediaTypeHeaderValue(mediaType);
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
