using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Exceptionless.Ingestion.Load;
using Xunit;

namespace Exceptionless.Tests.Benchmarks;

public sealed class IngestionLoadTests
{
    [Fact]
    public void ParseBothProtocolsAndBatchOptions()
    {
        LoadOptions options = LoadOptions.Parse([
            "--base-url", "https://localhost:7131/",
            "--project-id", "537650f3b77efe23a47914f4",
            "--submission-token", "submission",
            "--read-token", "read",
            "--protocol", "both",
            "--events", "1000",
            "--batch-size", "250",
            "--expected-persisted", "900",
            "--completion-poll-concurrency", "2"
        ]);

        Assert.Equal([IngestionProtocol.V2, IngestionProtocol.V3], options.Protocols);
        Assert.Equal(1000, options.EventCount);
        Assert.Equal(250, options.BatchSize);
        Assert.Equal(900, options.ExpectedPersisted);
        Assert.Equal(StackScenario.Hot, options.StackScenario);
        Assert.Equal(2, options.CompletionPollConcurrency);
    }

    [Fact]
    public void Parse_DiscardPercentWithoutOverride_DerivesExpectedPersisted()
    {
        LoadOptions options = LoadOptions.Parse([
            "--base-url", "https://localhost:7131/",
            "--project-id", "537650f3b77efe23a47914f4",
            "--submission-token", "submission",
            "--read-token", "read",
            "--protocol", "both",
            "--events", "250",
            "--discard-percent", "90"
        ]);

        Assert.Equal(20, options.ExpectedPersisted);
    }

    [Fact]
    public void Parse_NewStackDiscardComparison_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => LoadOptions.Parse([
            "--base-url", "https://localhost:7131/",
            "--project-id", "537650f3b77efe23a47914f4",
            "--submission-token", "submission",
            "--read-token", "read",
            "--protocol", "both",
            "--stack-scenario", "new",
            "--discard-percent", "10"
        ]));

        Assert.Contains("stack-scenario hot", exception.Message);
    }

    [Fact]
    public void Parse_V3WithoutPersistedEvents_DoesNotRequireReadCredential()
    {
        LoadOptions options = LoadOptions.Parse([
            "--base-url", "https://localhost:7131/",
            "--project-id", "537650f3b77efe23a47914f4",
            "--submission-token", "submission",
            "--protocol", "v3",
            "--expected-persisted", "0"
        ]);

        Assert.Equal([IngestionProtocol.V3], options.Protocols);
        Assert.Null(options.ReadToken);
    }

    [Fact]
    public async Task V2SingleEventWritesOneObjectAsync()
    {
        byte[] payload = await SerializeAsync(IngestionProtocol.V2, count: 1);
        using JsonDocument json = JsonDocument.Parse(payload);

        Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
        Assert.Equal("benchmark-reference-00000000", json.RootElement.GetProperty("reference_id").GetString());
        Assert.Equal("benchmark-reference", json.RootElement.GetProperty("tags")[0].GetString());
        Assert.Equal("Load.test.ActiveException0", json.RootElement.GetProperty("data").GetProperty("@simple_error").GetProperty("type").GetString());
    }

    [Fact]
    public async Task V2BatchWritesOneArrayAsync()
    {
        byte[] payload = await SerializeAsync(IngestionProtocol.V2, count: 100);
        using JsonDocument json = JsonDocument.Parse(payload);

        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.Equal(100, json.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task V3BatchWritesTopLevelValuesAsync()
    {
        byte[] payload = await SerializeAsync(IngestionProtocol.V3, count: 100);
        string[] lines = System.Text.Encoding.UTF8.GetString(payload).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(100, lines.Length);
        using JsonDocument first = JsonDocument.Parse(lines[0]);
        Assert.Equal("benchmark-reference-00000000", first.RootElement.GetProperty("reference_id").GetString());
        Assert.Equal("benchmark-reference", first.RootElement.GetProperty("tags")[0].GetString());
        Assert.Equal("Load.test.ActiveException0", first.RootElement.GetProperty("exception_type").GetString());
    }

    [Fact]
    public async Task GzipTracksRawAndTransferredBytesAsync()
    {
        LoadOptions options = CreateOptions() with { Compression = "gzip" };
        using var content = new StreamingEventContent(options, IngestionProtocol.V2, "benchmark-reference", "test", DateTimeOffset.UtcNow, 0, 100);
        byte[] compressed = await content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        await using var source = new MemoryStream(compressed);
        await using var gzip = new GZipStream(source, CompressionMode.Decompress);
        await using var uncompressed = new MemoryStream();
        await gzip.CopyToAsync(uncompressed, TestContext.Current.CancellationToken);

        Assert.Equal(compressed.Length, content.TransferredBytes);
        Assert.Equal(uncompressed.Length, content.UncompressedBytes);
        Assert.True(content.TransferredBytes < content.UncompressedBytes);
    }

    [Fact]
    public async Task RunAsync_V2AllDiscarded_WaitsForTrackedTerminalProcessing()
    {
        var handler = new CompletedV2Handler(observedPersisted: 0);
        LoadOptions options = CreateOptions() with
        {
            Protocols = [IngestionProtocol.V2],
            EventCount = 4,
            ExpectedPersisted = 0,
            BatchSize = 1,
            Trials = 1,
            WarmupEvents = 0,
            DiscardPercent = 100
        };

        int result = await new IngestionLoadRunner(options, handler).RunAsync();

        Assert.Equal(0, result);
        Assert.Equal(4, handler.IngestionRequests);
        Assert.True(handler.StatusRequests > 0);
        Assert.Equal(0, handler.CountRequests);
    }

    [Fact]
    public async Task RunAsync_V2MixedDiscarded_WaitsForTerminalAndQueryVisibleCompletion()
    {
        var handler = new CompletedV2Handler(observedPersisted: 50);
        LoadOptions options = CreateOptions() with
        {
            Protocols = [IngestionProtocol.V2],
            EventCount = 100,
            ExpectedPersisted = 50,
            BatchSize = 100,
            Trials = 1,
            WarmupEvents = 0,
            DiscardPercent = 50
        };

        int result = await new IngestionLoadRunner(options, handler).RunAsync();

        Assert.Equal(0, result);
        Assert.Equal(1, handler.IngestionRequests);
        Assert.True(handler.StatusRequests > 0);
        Assert.True(handler.CountRequests > 0);
    }

    [Fact]
    public async Task RunAsync_V3MixedDiscarded_WaitsForFullProcessingAndQueryVisibility()
    {
        var handler = new CompletedV3Handler(observedPersisted: 50);
        LoadOptions options = CreateOptions() with
        {
            Protocols = [IngestionProtocol.V3],
            EventCount = 100,
            ExpectedPersisted = 50,
            BatchSize = 100,
            Trials = 1,
            WarmupEvents = 0,
            DiscardPercent = 50
        };

        int result = await new IngestionLoadRunner(options, handler).RunAsync();

        Assert.Equal(0, result);
        Assert.Equal(1, handler.IngestionRequests);
        Assert.True(handler.StatusRequests >= 2);
        Assert.True(handler.CountRequests > 0);
    }

    [Fact]
    public async Task RunAsync_V3LargeCompletionSet_BoundsObserverConcurrencyAndStopsPollingCompletedChunks()
    {
        var handler = new CompletedV3Handler(observedPersisted: 2500, statusDelay: TimeSpan.FromMilliseconds(10));
        LoadOptions options = CreateOptions() with
        {
            Protocols = [IngestionProtocol.V3],
            EventCount = 2500,
            ExpectedPersisted = 2500,
            BatchSize = 2500,
            Trials = 1,
            WarmupEvents = 0,
            CompletionPollConcurrency = 2
        };

        int result = await new IngestionLoadRunner(options, handler).RunAsync();

        Assert.Equal(0, result);
        Assert.Equal(1, handler.IngestionRequests);
        Assert.Equal(4, handler.StatusRequests);
        Assert.InRange(handler.MaximumConcurrentStatusRequests, 1, 2);
        Assert.InRange(handler.StatusIdentifierReads, 2501, 3500);
    }

    [Fact]
    public async Task RunAsync_WithResultsPath_WritesSecretFreeEvidence()
    {
        string resultsPath = Path.Combine(Path.GetTempPath(), $"exceptionless-ingestion-{Guid.NewGuid():N}.json");
        try
        {
            var handler = new CompletedV2Handler(observedPersisted: 0);
            LoadOptions options = CreateOptions() with
            {
                Protocols = [IngestionProtocol.V2],
                EventCount = 1,
                ExpectedPersisted = 0,
                BatchSize = 1,
                Trials = 1,
                WarmupEvents = 0,
                DiscardPercent = 100,
                SubmissionToken = "secret-submission-token-123",
                ReadToken = "secret-read-token-456",
                ResultsPath = resultsPath,
                EnvironmentLabel = "test topology"
            };

            Assert.Equal(0, await new IngestionLoadRunner(options, handler).RunAsync());

            string json = await File.ReadAllTextAsync(resultsPath, TestContext.Current.CancellationToken);
            using JsonDocument evidence = JsonDocument.Parse(json);
            Assert.Equal("3", evidence.RootElement.GetProperty("schema_version").GetString());
            Assert.Equal("test topology", evidence.RootElement.GetProperty("environment").GetProperty("label").GetString());
            JsonElement recordedResult = evidence.RootElement.GetProperty("results")[0];
            Assert.Equal("event_post", recordedResult.GetProperty("completion_identifier_kind").GetString());
            Assert.Equal(1, recordedResult.GetProperty("completion_tracked_identifiers").GetInt64());
            Assert.True(recordedResult.GetProperty("completion_status_requests").GetInt32() >= 1);
            Assert.True(recordedResult.GetProperty("completion_identifier_reads").GetInt64() >= 1);
            Assert.DoesNotContain(options.SubmissionToken, json, StringComparison.Ordinal);
            Assert.DoesNotContain(options.ReadToken!, json, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(resultsPath);
        }
    }

    private static async Task<byte[]> SerializeAsync(IngestionProtocol protocol, int count)
    {
        using var content = new StreamingEventContent(CreateOptions(), protocol, "benchmark-reference", "test", DateTimeOffset.UtcNow, 0, count);
        return await content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
    }

    private static LoadOptions CreateOptions() => new(
        new Uri("https://localhost:7131/"),
        "537650f3b77efe23a47914f4",
        "submission",
        "read",
        null,
        null,
        [IngestionProtocol.V2, IngestionProtocol.V3],
        LoadEventType.Error,
        StackScenario.Hot,
        100,
        100,
        4,
        100,
        3,
        0,
        10,
        0,
        "none",
        "test",
        null,
        null,
        new string('x', 64),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMilliseconds(10),
        4);

    private sealed class CompletedV2Handler(long observedPersisted) : HttpMessageHandler
    {
        private int _nextId;
        private int _ingestionRequests;
        private int _statusRequests;
        private int _countRequests;
        public int IngestionRequests => Volatile.Read(ref _ingestionRequests);
        public int StatusRequests => Volatile.Read(ref _statusRequests);
        public int CountRequests => Volatile.Read(ref _countRequests);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/events/posts/status", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _statusRequests);
                await using Stream body = await request.Content!.ReadAsStreamAsync(cancellationToken);
                using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
                int requested = document.RootElement.GetProperty("ids").GetArrayLength();
                return JsonResponse(new { requested, queued = 0, completed = requested, unknown = 0 });
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/events/count", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _countRequests);
                return JsonResponse(new { total = observedPersisted });
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/events", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _ingestionRequests);
                _ = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
                var response = new HttpResponseMessage(HttpStatusCode.Accepted);
                response.Headers.Add("X-Exceptionless-Event-Post-Id", $"post-{Interlocked.Increment(ref _nextId)}");
                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage JsonResponse<T>(T value)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class CompletedV3Handler(long observedPersisted, TimeSpan? statusDelay = null) : HttpMessageHandler
    {
        private int _ingestionRequests;
        private int _statusRequests;
        private int _countRequests;
        private int _activeStatusRequests;
        private int _maximumConcurrentStatusRequests;
        private long _statusIdentifierReads;
        public int IngestionRequests => Volatile.Read(ref _ingestionRequests);
        public int StatusRequests => Volatile.Read(ref _statusRequests);
        public int CountRequests => Volatile.Read(ref _countRequests);
        public int MaximumConcurrentStatusRequests => Volatile.Read(ref _maximumConcurrentStatusRequests);
        public long StatusIdentifierReads => Volatile.Read(ref _statusIdentifierReads);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/events/processing/status", StringComparison.Ordinal))
            {
                int active = Interlocked.Increment(ref _activeStatusRequests);
                UpdateMaximum(ref _maximumConcurrentStatusRequests, active);
                try
                {
                    int statusRequest = Interlocked.Increment(ref _statusRequests);
                    await using Stream body = await request.Content!.ReadAsStreamAsync(cancellationToken);
                    using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
                    int requested = document.RootElement.GetProperty("client_ids").GetArrayLength();
                    Interlocked.Add(ref _statusIdentifierReads, requested);
                    if (statusDelay.HasValue)
                        await Task.Delay(statusDelay.Value, cancellationToken);
                    return JsonResponse(statusRequest == 1
                        ? new { requested, pending = requested, completed = 0 }
                        : new { requested, pending = 0, completed = requested });
                }
                finally
                {
                    Interlocked.Decrement(ref _activeStatusRequests);
                }
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/events/count", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _countRequests);
                return JsonResponse(new { total = observedPersisted });
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/events", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _ingestionRequests);
                string payload = await request.Content!.ReadAsStringAsync(cancellationToken);
                int received = payload.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
                return JsonResponse(new
                {
                    received,
                    persisted = (int)observedPersisted,
                    discarded = received - (int)observedPersisted,
                    duplicate = 0,
                    blocked = 0,
                    invalid = 0,
                    errors = Array.Empty<object>()
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage JsonResponse<T>(T value)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json")
            };
        }

        private static void UpdateMaximum(ref int target, int value)
        {
            int current = Volatile.Read(ref target);
            while (value > current)
            {
                int observed = Interlocked.CompareExchange(ref target, value, current);
                if (observed == current)
                    return;
                current = observed;
            }
        }
    }
}
