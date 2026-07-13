using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Serialization;

namespace Exceptionless.Ingestion.Load;

internal sealed class IngestionLoadRunner
{
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
        Console.WriteLine($"Comparing {String.Join(" and ", _options.Protocols)}: event_type={_options.EventType.ToString().ToLowerInvariant()} events={_options.EventCount} batch_size={_options.BatchSize} requests={(int)Math.Ceiling((double)_options.EventCount / _options.BatchSize)} concurrency={_options.Concurrency} trials={_options.Trials} compression={_options.Compression}");
        Console.WriteLine("Submission stops at the last HTTP response; processed includes V2 queue drain or V3 terminal persistence.");

        if (_options.WarmupEvents > 0)
        {
            foreach (IngestionProtocol protocol in _options.Protocols)
            {
                Console.WriteLine($"Warming {protocol} with {_options.WarmupEvents} events...");
                await ExecuteAsync(protocol, trial: -1, _options.WarmupEvents, _options.WarmupEvents, isWarmup: true);
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
        _client.Dispose();
        return 0;
    }

    private async Task<LoadRunResult> ExecuteAsync(IngestionProtocol protocol, int trial, int eventCount, int expectedPersisted, bool isWarmup)
    {
        using var cancellation = new CancellationTokenSource(_options.Timeout);
        string phase = isWarmup ? "warm" : $"t{trial + 1}";
        string runMarker = $"load-{_options.Seed}-{protocol.ToString().ToLowerInvariant()}-{phase}-{Guid.NewGuid():N}";
        DateTimeOffset eventDate = DateTimeOffset.UtcNow;
        int requestCount = (eventCount + _options.BatchSize - 1) / _options.BatchSize;
        int nextRequest = -1;
        var requestLatencies = new ConcurrentBag<double>();
        var totals = new LoadTotals();
        var stopwatch = Stopwatch.StartNew();

        Task[] workers = Enumerable.Range(0, Math.Min(_options.Concurrency, requestCount)).Select(_ => Task.Run(async () =>
        {
            while (true)
            {
                int requestIndex = Interlocked.Increment(ref nextRequest);
                if (requestIndex >= requestCount)
                    return;

                int start = requestIndex * _options.BatchSize;
                int count = Math.Min(_options.BatchSize, eventCount - start);
                using var content = new StreamingEventContent(_options, protocol, runMarker, eventDate, start, count);
                using var request = new HttpRequestMessage(HttpMethod.Post, GetIngestionUrl(protocol)) { Content = content };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SubmissionToken);
                long started = Stopwatch.GetTimestamp();
                using HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
                requestLatencies.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                if (!response.IsSuccessStatusCode)
                {
                    string detail = await response.Content.ReadAsStringAsync(cancellation.Token);
                    throw new InvalidOperationException($"{protocol} request {requestIndex + 1} returned {(int)response.StatusCode}: {Limit(detail)}");
                }

                if (protocol is IngestionProtocol.V3)
                {
                    await using Stream body = await response.Content.ReadAsStreamAsync(cancellation.Token);
                    EventIngestionV3Response? terminal = await JsonSerializer.DeserializeAsync(body, EventIngestionJsonContext.Default.EventIngestionV3Response, cancellation.Token);
                    if (terminal is null)
                        throw new InvalidOperationException($"V3 request {requestIndex + 1} returned an empty terminal response.");
                    totals.Add(terminal);
                }

                Interlocked.Add(ref totals.UncompressedBytes, content.UncompressedBytes);
                Interlocked.Add(ref totals.TransferredBytes, content.TransferredBytes);
                Interlocked.Increment(ref totals.SuccessfulRequests);
            }
        }, cancellation.Token)).ToArray();

        await Task.WhenAll(workers);
        TimeSpan submissionElapsed = stopwatch.Elapsed;
        long observed;
        if (protocol is IngestionProtocol.V2)
        {
            observed = await WaitForProcessedCountAsync(runMarker, expectedPersisted, cancellation.Token);
        }
        else
        {
            observed = totals.Persisted;
            if (observed < expectedPersisted)
                throw new InvalidOperationException($"V3 terminal responses reported {observed} persisted events; expected at least {expectedPersisted}.");
        }
        stopwatch.Stop();

        return new LoadRunResult(
            protocol,
            trial,
            eventCount,
            expectedPersisted,
            requestCount,
            totals.SuccessfulRequests,
            totals.UncompressedBytes,
            totals.TransferredBytes,
            submissionElapsed,
            stopwatch.Elapsed,
            observed,
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

    private async Task<long> WaitForProcessedCountAsync(string runMarker, int expectedPersisted, CancellationToken cancellationToken)
    {
        if (expectedPersisted == 0)
            return 0;

        Uri url = new(_options.BaseUrl, $"api/v2/projects/{_options.ProjectId}/events/count?filter={Uri.EscapeDataString($"tag:{runMarker}")}");
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            SetReadAuthorization(request);
            using HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string detail = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"V2 processed count query returned {(int)response.StatusCode}: {Limit(detail)}");
            }

            await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            long total = json.RootElement.GetProperty("total").GetInt64();
            if (total >= expectedPersisted)
                return total;
            await Task.Delay(_options.PollInterval, cancellationToken);
        }
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

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        double[] ordered = values.Order().ToArray();
        if (ordered.Length == 0)
            return 0;
        int index = (int)Math.Ceiling(percentile * ordered.Length) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Length - 1)];
    }

    private static string Limit(string value) => value.Length <= 500 ? value : value[..500];

    private static void WriteResult(LoadRunResult result)
    {
        double submissionRate = result.EventCount / Math.Max(result.SubmissionElapsed.TotalSeconds, 0.001);
        double processedRate = result.ExpectedPersisted / Math.Max(result.ProcessedElapsed.TotalSeconds, 0.001);
        Console.WriteLine($"{result.Protocol} trial={result.Trial + 1} requests={result.SuccessfulRequests}/{result.RequestCount} submission={submissionRate:F0} events/s processed={processedRate:F0} events/s ack={result.SubmissionElapsed.TotalSeconds:F3}s drain={result.DrainElapsed.TotalSeconds:F3}s total={result.ProcessedElapsed.TotalSeconds:F3}s observed={result.ObservedPersisted} latency_ms_p50/p95/p99={result.P50Milliseconds:F1}/{result.P95Milliseconds:F1}/{result.P99Milliseconds:F1} bytes={result.TransferredBytes} raw_bytes={result.UncompressedBytes}");
        if (result.Protocol is IngestionProtocol.V3)
            Console.WriteLine($"  terminal received={result.Received} persisted={result.Persisted} discarded={result.Discarded} duplicate={result.Duplicate} blocked={result.Blocked} invalid={result.Invalid}");
    }

    private static void WriteSummary(IReadOnlyList<LoadRunResult> results)
    {
        Console.WriteLine("Median measured results:");
        foreach (IGrouping<IngestionProtocol, LoadRunResult> group in results.GroupBy(r => r.Protocol).OrderBy(g => g.Key))
        {
            double submissionRate = Median(group.Select(r => r.EventCount / r.SubmissionElapsed.TotalSeconds));
            double processedRate = Median(group.Select(r => r.ExpectedPersisted / r.ProcessedElapsed.TotalSeconds));
            double drain = Median(group.Select(r => r.DrainElapsed.TotalSeconds));
            Console.WriteLine($"  {group.Key}: submission={submissionRate:F0} events/s processed={processedRate:F0} events/s drain={drain:F3}s");
        }
    }

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

internal sealed record LoadRunResult(
    IngestionProtocol Protocol,
    int Trial,
    int EventCount,
    int ExpectedPersisted,
    int RequestCount,
    long SuccessfulRequests,
    long UncompressedBytes,
    long TransferredBytes,
    TimeSpan SubmissionElapsed,
    TimeSpan ProcessedElapsed,
    long ObservedPersisted,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    long Received,
    long Persisted,
    long Discarded,
    long Duplicate,
    long Blocked,
    long Invalid)
{
    public TimeSpan DrainElapsed => ProcessedElapsed - SubmissionElapsed;
}
