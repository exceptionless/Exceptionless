using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Serialization;

namespace Exceptionless.Ingestion.Load;

internal sealed class IngestionLoadRunner
{
    private const string EventPostIdHeader = "X-Exceptionless-Event-Post-Id";
    private const string TrackEventPostHeader = "X-Exceptionless-Track-Event-Post";
    private static readonly JsonSerializerOptions _protocolJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly LoadOptions _options;
    private readonly HttpClient _client;

    public IngestionLoadRunner(LoadOptions options, HttpMessageHandler? handler = null)
    {
        _options = options;
        _client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _client.Timeout = Timeout.InfiniteTimeSpan;
        _client.DefaultRequestVersion = HttpVersion.Version20;
        _client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
    }

    public async Task<int> RunAsync()
    {
        Console.WriteLine($"Comparing {String.Join(" and ", _options.Protocols)}: event_type={_options.EventType.ToString().ToLowerInvariant()} stack_scenario={_options.StackScenario.ToString().ToLowerInvariant()} events={_options.EventCount} expected_persisted={_options.ExpectedPersisted} batch_size={_options.BatchSize} requests={(int)Math.Ceiling((double)_options.EventCount / _options.BatchSize)} concurrency={_options.Concurrency} trials={_options.Trials} compression={_options.Compression} completion_poll_concurrency={_options.CompletionPollConcurrency}");
        Console.WriteLine("Submission headers and query visibility use common boundaries. Instrumented terminal processing includes observer counters because V2 tracks requests while V3 tracks persisted events; V3 durable acknowledgement is reported separately.");

        if (_options.WarmupEvents > 0)
        {
            foreach (IngestionProtocol protocol in _options.Protocols)
            {
                Console.WriteLine($"Warming {protocol} with {_options.WarmupEvents} events...");
                await ExecuteAsync(protocol, trial: -1, _options.WarmupEvents, GetExpectedPersistedCount(_options.WarmupEvents), isWarmup: true);
            }
        }

        var results = new List<LoadRunResult>();
        for (int trial = 0; trial < _options.Trials; trial++)
        {
            IEnumerable<IngestionProtocol> order = trial % 2 == 0 ? _options.Protocols : _options.Protocols.Reverse();
            foreach (IngestionProtocol protocol in order)
            {
                LoadRunResult result = await ExecuteAsync(protocol, trial, _options.EventCount, _options.ExpectedPersisted, isWarmup: false);
                results.Add(result);
                WriteResult(result);
            }
        }

        WriteSummary(results);
        if (!String.IsNullOrWhiteSpace(_options.ResultsPath))
            await WriteEvidenceAsync(results, _options.ResultsPath);

        _client.Dispose();
        return 0;
    }

    private async Task<LoadRunResult> ExecuteAsync(IngestionProtocol protocol, int trial, int eventCount, int expectedPersisted, bool isWarmup)
    {
        using var cancellation = new CancellationTokenSource(_options.Timeout);
        string phase = isWarmup ? "warm" : $"t{trial + 1}";
        string runMarker = $"load-{_options.Seed}-{protocol.ToString().ToLowerInvariant()}-{phase}-{Guid.NewGuid():N}";
        string signatureNamespace = GetSignatureNamespace(protocol, phase, isWarmup);
        string[] expectedV3ClientIds = protocol is IngestionProtocol.V3 && expectedPersisted > 0
            ? GetExpectedV3ClientIds(runMarker, eventCount, expectedPersisted)
            : [];
        DateTimeOffset eventDate = DateTimeOffset.UtcNow;
        int requestCount = (eventCount + _options.BatchSize - 1) / _options.BatchSize;
        int nextRequest = -1;
        var requestLatencies = new ConcurrentBag<double>();
        var processingCorrelationIds = new ConcurrentBag<string>();
        var totals = new LoadTotals();
        long runStarted = Stopwatch.GetTimestamp();
        long lastSubmissionResponse = runStarted;
        long lastDurableAcknowledgement = runStarted;

        Task[] workers = Enumerable.Range(0, Math.Min(_options.Concurrency, requestCount)).Select(_ => Task.Run(async () =>
        {
            while (true)
            {
                int requestIndex = Interlocked.Increment(ref nextRequest);
                if (requestIndex >= requestCount)
                    return;

                int start = requestIndex * _options.BatchSize;
                int count = Math.Min(_options.BatchSize, eventCount - start);
                using var content = new StreamingEventContent(_options, protocol, runMarker, signatureNamespace, eventDate, start, count);
                using var request = new HttpRequestMessage(HttpMethod.Post, GetIngestionUrl(protocol)) { Content = content };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SubmissionToken);
                if (protocol is IngestionProtocol.V2)
                    request.Headers.Add(TrackEventPostHeader, "true");

                long started = Stopwatch.GetTimestamp();
                using HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
                long responseReceived = Stopwatch.GetTimestamp();
                UpdateMaximum(ref lastSubmissionResponse, responseReceived);
                requestLatencies.Add(Stopwatch.GetElapsedTime(started, responseReceived).TotalMilliseconds);
                if (!response.IsSuccessStatusCode)
                {
                    string detail = await response.Content.ReadAsStringAsync(cancellation.Token);
                    throw new InvalidOperationException($"{protocol} request {requestIndex + 1} returned {(int)response.StatusCode}: {Limit(detail)}");
                }

                if (protocol is IngestionProtocol.V2)
                {
                    if (!response.Headers.TryGetValues(EventPostIdHeader, out var values) || values.SingleOrDefault() is not { Length: > 0 } processingCorrelationId)
                        throw new InvalidOperationException($"V2 request {requestIndex + 1} did not return the required {EventPostIdHeader} tracking header.");
                    processingCorrelationIds.Add(processingCorrelationId);
                }
                else
                {
                    await using Stream body = await response.Content.ReadAsStreamAsync(cancellation.Token);
                    EventIngestionV3Response? terminal = await JsonSerializer.DeserializeAsync(body, EventIngestionJsonContext.Default.EventIngestionV3Response, cancellation.Token);
                    if (terminal is null)
                        throw new InvalidOperationException($"V3 request {requestIndex + 1} returned an empty terminal response.");
                    totals.Add(terminal);
                    UpdateMaximum(ref lastDurableAcknowledgement, Stopwatch.GetTimestamp());
                }

                Interlocked.Add(ref totals.UncompressedBytes, content.UncompressedBytes);
                Interlocked.Add(ref totals.TransferredBytes, content.TransferredBytes);
                Interlocked.Increment(ref totals.SuccessfulRequests);
            }
        }, cancellation.Token)).ToArray();

        await Task.WhenAll(workers);
        TimeSpan submissionElapsed = Stopwatch.GetElapsedTime(runStarted, Volatile.Read(ref lastSubmissionResponse));

        if (protocol is IngestionProtocol.V3)
        {
            if (totals.Received != eventCount)
                throw new InvalidOperationException($"V3 terminal responses reported {totals.Received} received events; expected {eventCount}.");
            if (!isWarmup && totals.Persisted != expectedPersisted)
                throw new InvalidOperationException($"V3 terminal responses reported {totals.Persisted} persisted events; expected {expectedPersisted}.");
        }

        TimeSpan? durableAcknowledgementElapsed = protocol is IngestionProtocol.V3
            ? Stopwatch.GetElapsedTime(runStarted, Volatile.Read(ref lastDurableAcknowledgement))
            : null;
        Task<CompletionObservation> fullProcessingTask = protocol is IngestionProtocol.V2
            ? WaitForV2PipelineCompletionAsync(processingCorrelationIds.ToArray(), requestCount, runStarted, cancellation.Token)
            : expectedPersisted > 0
                ? WaitForV3FullProcessingAsync(expectedV3ClientIds, runStarted, cancellation.Token)
                : Task.FromResult(new CompletionObservation(durableAcknowledgementElapsed!.Value, 0, 0, 0, 0));
        Task<QueryVisibilityObservation?> queryVisibilityTask = !isWarmup && expectedPersisted > 0
            ? WaitForQueryVisibilityAsync(runMarker, expectedPersisted, runStarted, cancellation.Token)
            : Task.FromResult<QueryVisibilityObservation?>(null);

        CompletionObservation fullProcessing = await fullProcessingTask;
        QueryVisibilityObservation? queryVisibility = await queryVisibilityTask;

        return new LoadRunResult(
            protocol,
            trial,
            runMarker,
            signatureNamespace,
            eventCount,
            expectedPersisted,
            requestCount,
            totals.SuccessfulRequests,
            totals.UncompressedBytes,
            totals.TransferredBytes,
            submissionElapsed,
            fullProcessing.Elapsed,
            durableAcknowledgementElapsed,
            queryVisibility?.Elapsed,
            queryVisibility?.ObservedPersisted ?? 0,
            protocol is IngestionProtocol.V2
                ? CompletionIdentifierKind.EventPost
                : expectedPersisted > 0
                    ? CompletionIdentifierKind.PersistedEvent
                    : CompletionIdentifierKind.None,
            fullProcessing.TrackedIdentifiers,
            fullProcessing.StatusRequests,
            fullProcessing.IdentifierReads,
            fullProcessing.Sweeps,
            queryVisibility?.Requests ?? 0,
            Percentile(requestLatencies, 0.50),
            Percentile(requestLatencies, 0.95),
            Percentile(requestLatencies, 0.99),
            totals.Received,
            totals.Persisted,
            totals.Discarded,
            totals.Duplicate,
            totals.Blocked,
            totals.Invalid);
    }

    private Task<CompletionObservation> WaitForV3FullProcessingAsync(string[] clientIds, long runStarted, CancellationToken cancellationToken)
    {
        string[][] chunks = clientIds.Chunk(1000).Select(chunk => chunk.ToArray()).ToArray();
        return WaitForCompletionAsync(
            chunks,
            runStarted,
            GetV3ProcessingSummaryAsync,
            static (chunk, summary) => summary.Requested == chunk.Length && summary.Completed == chunk.Length,
            cancellationToken);
    }

    private async Task<EventIngestionV3ProcessingSummary> GetV3ProcessingSummaryAsync(string[] clientIds, CancellationToken cancellationToken)
    {
        Uri url = new(_options.BaseUrl, $"api/v3/projects/{_options.ProjectId}/events/processing/status");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SubmissionToken);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new EventIngestionV3ProcessingStatusRequest(clientIds), _protocolJsonOptions);
        request.Content = new ByteArrayContent(payload);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"V3 full-processing status returned {(int)response.StatusCode}: {Limit(detail)}");
        }

        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<EventIngestionV3ProcessingSummary>(body, _protocolJsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("V3 full-processing status returned an empty response.");
    }

    private Task<CompletionObservation> WaitForV2PipelineCompletionAsync(string[] processingCorrelationIds, int requestCount, long runStarted, CancellationToken cancellationToken)
    {
        if (processingCorrelationIds.Length != requestCount)
            throw new InvalidOperationException($"V2 returned {processingCorrelationIds.Length} tracked event-post ids for {requestCount} successful requests.");

        string[][] chunks = processingCorrelationIds.Chunk(1000).Select(chunk => chunk.ToArray()).ToArray();
        return WaitForCompletionAsync(
            chunks,
            runStarted,
            GetV2ProcessingSummaryAsync,
            static (chunk, summary) => summary.Requested == chunk.Length && summary.Completed == chunk.Length,
            cancellationToken);
    }

    private async Task<CompletionObservation> WaitForCompletionAsync<TSummary>(
        string[][] chunks,
        long runStarted,
        Func<string[], CancellationToken, Task<TSummary>> getSummaryAsync,
        Func<string[], TSummary, bool> isComplete,
        CancellationToken cancellationToken)
    {
        var pending = chunks.ToList();
        int statusRequests = 0;
        long identifierReads = 0;
        int sweeps = 0;
        while (pending.Count > 0)
        {
            sweeps++;
            var nextPending = new List<string[]>();
            foreach (string[][] page in pending.Chunk(_options.CompletionPollConcurrency))
            {
                TSummary[] summaries = await Task.WhenAll(page.Select(chunk => getSummaryAsync(chunk, cancellationToken)));
                statusRequests += page.Length;
                identifierReads += page.Sum(chunk => (long)chunk.Length);
                for (int index = 0; index < page.Length; index++)
                {
                    if (!isComplete(page[index], summaries[index]))
                        nextPending.Add(page[index]);
                }
            }

            if (nextPending.Count == 0)
            {
                return new CompletionObservation(
                    Stopwatch.GetElapsedTime(runStarted),
                    chunks.Sum(chunk => (long)chunk.Length),
                    statusRequests,
                    identifierReads,
                    sweeps);
            }

            pending = nextPending;
            await Task.Delay(_options.PollInterval, cancellationToken);
        }

        throw new InvalidOperationException("At least one completion identifier is required.");
    }

    private async Task<EventPostProcessingSummary> GetV2ProcessingSummaryAsync(string[] processingCorrelationIds, CancellationToken cancellationToken)
    {
        Uri url = new(_options.BaseUrl, $"api/v2/projects/{_options.ProjectId}/events/posts/status");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SubmissionToken);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new EventPostProcessingStatusRequest(processingCorrelationIds), _protocolJsonOptions);
        request.Content = new ByteArrayContent(payload);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"V2 processing status returned {(int)response.StatusCode}: {Limit(detail)}");
        }

        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<EventPostProcessingSummary>(body, _protocolJsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("V2 processing status returned an empty response.");
    }

    private async Task<QueryVisibilityObservation?> WaitForQueryVisibilityAsync(string runMarker, int expectedPersisted, long runStarted, CancellationToken cancellationToken)
    {
        Uri url = new(_options.BaseUrl, $"api/v2/projects/{_options.ProjectId}/events/count?filter={Uri.EscapeDataString($"tag:{runMarker}")}");
        int requests = 0;
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            SetReadAuthorization(request);
            using HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            requests++;
            if (!response.IsSuccessStatusCode)
            {
                string detail = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Query-visible count returned {(int)response.StatusCode}: {Limit(detail)}");
            }

            await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            long total = json.RootElement.GetProperty("total").GetInt64();
            if (total >= expectedPersisted)
                return new QueryVisibilityObservation(Stopwatch.GetElapsedTime(runStarted), total, requests);
            await Task.Delay(_options.PollInterval, cancellationToken);
        }
    }

    private string GetSignatureNamespace(IngestionProtocol protocol, string phase, bool isWarmup)
    {
        string prefix = $"{_options.Seed}-{protocol.ToString().ToLowerInvariant()}";
        if (_options.StackScenario is StackScenario.Hot)
            return $"{prefix}-hot";

        return $"{prefix}-{phase}-{(isWarmup ? "warm" : "new")}-{Guid.NewGuid():N}";
    }

    private string[] GetExpectedV3ClientIds(string runMarker, int eventCount, int expectedPersisted)
    {
        IEnumerable<int> expectedIndexes = Enumerable.Range(0, eventCount);
        if (expectedPersisted != eventCount)
            expectedIndexes = expectedIndexes.Where(index => index % 100 >= _options.DiscardPercent);

        int[] indexes = expectedIndexes.ToArray();
        if (indexes.Length != expectedPersisted)
        {
            throw new InvalidOperationException(
                $"Cannot identify the {expectedPersisted} V3 events expected to persist from this corpus. " +
                "Use the discard-derived default, zero, or the full event count for --expected-persisted.");
        }

        return indexes.Select(index => StreamingEventContent.GetV3ClientId(runMarker, index)).ToArray();
    }

    private int GetExpectedPersistedCount(int eventCount)
    {
        if (_options.ExpectedPersisted == _options.EventCount)
            return eventCount;
        if (_options.ExpectedPersisted == 0)
            return 0;

        int discarded = eventCount / 100 * _options.DiscardPercent + Math.Min(eventCount % 100, _options.DiscardPercent);
        return eventCount - discarded;
    }

    private Uri GetIngestionUrl(IngestionProtocol protocol) => new(_options.BaseUrl, $"api/{protocol.ToString().ToLowerInvariant()}/projects/{_options.ProjectId}/events");

    private void SetReadAuthorization(HttpRequestMessage request)
    {
        if (!String.IsNullOrWhiteSpace(_options.ReadToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ReadToken);
            return;
        }

        string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ReadUser}:{_options.ReadPassword}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    private async Task WriteEvidenceAsync(IReadOnlyList<LoadRunResult> results, string resultsPath)
    {
        string fullPath = Path.GetFullPath(resultsPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!String.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var evidence = new LoadEvidence(
            "3",
            DateTimeOffset.UtcNow,
            new LoadEnvironment(
                _options.EnvironmentLabel,
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                System.Environment.ProcessorCount,
                typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion),
            new LoadConfiguration(
                _options.BaseUrl.ToString(),
                _options.ProjectId,
                _options.Protocols,
                _options.EventType,
                _options.StackScenario,
                _options.EventCount,
                _options.ExpectedPersisted,
                _options.Concurrency,
                _options.BatchSize,
                _options.Trials,
                _options.WarmupEvents,
                _options.SignatureCardinality,
                _options.DiscardPercent,
                _options.Compression,
                _options.Seed,
                _options.Message.Length,
                _options.Timeout.TotalSeconds,
                _options.PollInterval.TotalMilliseconds,
                _options.CompletionPollConcurrency),
            results);
        var serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        serializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        await using FileStream output = File.Create(fullPath);
        await JsonSerializer.SerializeAsync(output, evidence, serializerOptions);
        Console.WriteLine($"Evidence written to {fullPath}");
    }

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        double[] ordered = values.Order().ToArray();
        if (ordered.Length == 0)
            return 0;
        int index = (int)Math.Ceiling(percentile * ordered.Length) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Length - 1)];
    }

    private static string Limit(string value) => value.Length <= 500 ? value : value[..500];

    private static void UpdateMaximum(ref long target, long value)
    {
        long current = Volatile.Read(ref target);
        while (value > current)
        {
            long observed = Interlocked.CompareExchange(ref target, value, current);
            if (observed == current)
                return;
            current = observed;
        }
    }

    private static void WriteResult(LoadRunResult result)
    {
        double submissionRate = Rate(result.EventCount, result.SubmissionElapsed);
        string durableAcknowledgement = result.V3DurableAcknowledgementElapsed.HasValue
            ? $" durable_ack={Rate(result.EventCount, result.V3DurableAcknowledgementElapsed.Value):F0} events/s {result.V3DurableAcknowledgementElapsed.Value.TotalSeconds:F3}s"
            : String.Empty;
        string queryVisible = result.QueryVisibleElapsed.HasValue
            ? $"{Rate(result.ExpectedPersisted, result.QueryVisibleElapsed.Value):F0} persisted/s {result.QueryVisibleElapsed.Value.TotalSeconds:F3}s"
            : "n/a";
        Console.WriteLine($"{result.Protocol} trial={result.Trial + 1} requests={result.SuccessfulRequests}/{result.RequestCount} submission={submissionRate:F0} events/s full_processing_observed={Rate(result.EventCount, result.ObservedFullProcessingElapsed):F0} events/s {result.ObservedFullProcessingElapsed.TotalSeconds:F3}s{durableAcknowledgement} query_visible={queryVisible} observed_persisted={result.ObservedPersisted} latency_ms_p50/p95/p99={result.P50Milliseconds:F1}/{result.P95Milliseconds:F1}/{result.P99Milliseconds:F1} bytes={result.TransferredBytes} raw_bytes={result.UncompressedBytes}");
        string completionIdentifierKind = JsonNamingPolicy.SnakeCaseLower.ConvertName(result.CompletionIdentifierKind.ToString());
        Console.WriteLine($"  completion_observer identifier_kind={completionIdentifierKind} tracked_ids={result.CompletionTrackedIdentifiers} status_requests={result.CompletionStatusRequests} identifier_reads={result.CompletionIdentifierReads} sweeps={result.CompletionSweeps} query_requests={result.QueryVisibilityRequests}");
        if (result.Protocol is IngestionProtocol.V3)
            Console.WriteLine($"  terminal received={result.Received} persisted={result.Persisted} discarded={result.Discarded} duplicate={result.Duplicate} blocked={result.Blocked} invalid={result.Invalid}");
    }

    private static void WriteSummary(IReadOnlyList<LoadRunResult> results)
    {
        Console.WriteLine("Median measured results:");
        foreach (IGrouping<IngestionProtocol, LoadRunResult> group in results.GroupBy(r => r.Protocol).OrderBy(g => g.Key))
        {
            double submissionRate = Median(group.Select(r => Rate(r.EventCount, r.SubmissionElapsed)));
            double? queryVisibleRate = group.All(r => r.QueryVisibleElapsed.HasValue)
                ? Median(group.Select(r => Rate(r.ExpectedPersisted, r.QueryVisibleElapsed!.Value)))
                : null;
            double fullProcessingRate = Median(group.Select(r => Rate(r.EventCount, r.ObservedFullProcessingElapsed)));
            string durableAcknowledgement = group.Key is IngestionProtocol.V3
                ? $" durable_ack={Median(group.Select(r => Rate(r.EventCount, r.V3DurableAcknowledgementElapsed!.Value))):F0} events/s"
                : String.Empty;
            Console.WriteLine($"  {group.Key}: submission={submissionRate:F0} events/s full_processing_observed={fullProcessingRate:F0} events/s{durableAcknowledgement} query_visible={(queryVisibleRate.HasValue ? $"{queryVisibleRate:F0} persisted/s" : "n/a")}");
        }
        Console.WriteLine("Full-processing observations include protocol-specific tracking and polling overhead; use the recorded observer counters and server telemetry before attributing a difference to pipeline efficiency.");
    }

    private static double Rate(long count, TimeSpan elapsed) => count / Math.Max(elapsed.TotalSeconds, 0.001);

    private static double Median(IEnumerable<double> source)
    {
        double[] values = source.Order().ToArray();
        int middle = values.Length / 2;
        return values.Length % 2 == 0 ? (values[middle - 1] + values[middle]) / 2 : values[middle];
    }

    private sealed class LoadTotals
    {
        public long SuccessfulRequests;
        public long UncompressedBytes;
        public long TransferredBytes;
        public long Received;
        public long Persisted;
        public long Discarded;
        public long Duplicate;
        public long Blocked;
        public long Invalid;

        public void Add(EventIngestionV3Response value)
        {
            Interlocked.Add(ref Received, value.Received);
            Interlocked.Add(ref Persisted, value.Persisted);
            Interlocked.Add(ref Discarded, value.Discarded);
            Interlocked.Add(ref Duplicate, value.Duplicate);
            Interlocked.Add(ref Blocked, value.Blocked);
            Interlocked.Add(ref Invalid, value.Invalid);
        }
    }
}

internal sealed record EventPostProcessingStatusRequest(string[] Ids);
internal sealed record EventPostProcessingSummary(int Requested, int Queued, int Completed, int Unknown);
internal sealed record EventIngestionV3ProcessingStatusRequest(
    [property: JsonPropertyName("client_ids")] string[] ClientIds);
internal sealed record EventIngestionV3ProcessingSummary(int Requested, int Pending, int Completed);
internal sealed record CompletionObservation(
    TimeSpan Elapsed,
    long TrackedIdentifiers,
    int StatusRequests,
    long IdentifierReads,
    int Sweeps);
internal sealed record QueryVisibilityObservation(TimeSpan Elapsed, long ObservedPersisted, int Requests);

internal enum CompletionIdentifierKind
{
    None,
    EventPost,
    PersistedEvent
}

internal sealed record LoadRunResult(
    IngestionProtocol Protocol,
    int Trial,
    string RunMarker,
    string SignatureNamespace,
    int EventCount,
    int ExpectedPersisted,
    int RequestCount,
    long SuccessfulRequests,
    long UncompressedBytes,
    long TransferredBytes,
    TimeSpan SubmissionElapsed,
    TimeSpan ObservedFullProcessingElapsed,
    TimeSpan? V3DurableAcknowledgementElapsed,
    TimeSpan? QueryVisibleElapsed,
    long ObservedPersisted,
    CompletionIdentifierKind CompletionIdentifierKind,
    long CompletionTrackedIdentifiers,
    int CompletionStatusRequests,
    long CompletionIdentifierReads,
    int CompletionSweeps,
    int QueryVisibilityRequests,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    long Received,
    long Persisted,
    long Discarded,
    long Duplicate,
    long Blocked,
    long Invalid);

internal sealed record LoadEvidence(
    string SchemaVersion,
    DateTimeOffset CapturedUtc,
    LoadEnvironment Environment,
    LoadConfiguration Configuration,
    IReadOnlyList<LoadRunResult> Results);

internal sealed record LoadEnvironment(
    string? Label,
    string Runtime,
    string OperatingSystem,
    string ProcessArchitecture,
    int ProcessorCount,
    string? BuildVersion);

internal sealed record LoadConfiguration(
    string BaseUrl,
    string ProjectId,
    IReadOnlyList<IngestionProtocol> Protocols,
    LoadEventType EventType,
    StackScenario StackScenario,
    int EventCount,
    int ExpectedPersisted,
    int Concurrency,
    int BatchSize,
    int Trials,
    int WarmupEvents,
    int SignatureCardinality,
    int DiscardPercent,
    string Compression,
    string Seed,
    int MessageBytes,
    double TimeoutSeconds,
    double PollIntervalMilliseconds,
    int CompletionPollConcurrency);
