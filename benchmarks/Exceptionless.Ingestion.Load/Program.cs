using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Serialization;

namespace Exceptionless.Ingestion.Load;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        LoadOptions options;
        try
        {
            options = LoadOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            LoadOptions.WriteUsage();
            return 2;
        }

        using var client = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan,
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        int segmentCount = (options.EventCount + options.SegmentSize - 1) / options.SegmentSize;
        int nextSegment = -1;
        var totals = new LoadTotals();
        using var cancellation = new CancellationTokenSource(options.Timeout);
        var stopwatch = Stopwatch.StartNew();

        Task[] workers = Enumerable.Range(0, options.Concurrency).Select(_ => Task.Run(async () =>
        {
            while (true)
            {
                int segment = Interlocked.Increment(ref nextSegment);
                if (segment >= segmentCount)
                    return;

                int start = segment * options.SegmentSize;
                int count = Math.Min(options.SegmentSize, options.EventCount - start);
                using var request = new HttpRequestMessage(HttpMethod.Post, options.Url)
                {
                    Content = new StreamingEventContent(options, start, count)
                };
                try
                {
                    using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
                    if (!response.IsSuccessStatusCode)
                    {
                        Interlocked.Increment(ref totals.FailedSegments);
                        continue;
                    }

                    await using Stream body = await response.Content.ReadAsStreamAsync(cancellation.Token);
                    EventIngestionV3Response? result = await JsonSerializer.DeserializeAsync(
                        body,
                        EventIngestionJsonContext.Default.EventIngestionV3Response,
                        cancellation.Token);
                    if (result is null)
                    {
                        Interlocked.Increment(ref totals.FailedSegments);
                        continue;
                    }

                    Interlocked.Add(ref totals.Received, result.Received);
                    Interlocked.Add(ref totals.Persisted, result.Persisted);
                    Interlocked.Add(ref totals.Discarded, result.Discarded);
                    Interlocked.Add(ref totals.Duplicate, result.Duplicate);
                    Interlocked.Add(ref totals.Blocked, result.Blocked);
                    Interlocked.Add(ref totals.Invalid, result.Invalid);
                    Interlocked.Increment(ref totals.SuccessfulSegments);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    Interlocked.Increment(ref totals.FailedSegments);
                    return;
                }
                catch (HttpRequestException)
                {
                    Interlocked.Increment(ref totals.FailedSegments);
                }
            }
        }, cancellation.Token)).ToArray();

        try
        {
            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        stopwatch.Stop();

        double eventsPerSecond = totals.Received / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
        Console.WriteLine($"elapsed={stopwatch.Elapsed:c} events_per_second={eventsPerSecond:F0} successful_segments={totals.SuccessfulSegments} failed_segments={totals.FailedSegments}");
        Console.WriteLine($"received={totals.Received} persisted={totals.Persisted} discarded={totals.Discarded} duplicate={totals.Duplicate} blocked={totals.Blocked} invalid={totals.Invalid}");
        return totals.FailedSegments == 0 ? 0 : 1;
    }

    private sealed class StreamingEventContent : HttpContent
    {
        private static readonly byte[] _newline = [(byte)'\n'];
        private readonly LoadOptions _options;
        private readonly int _start;
        private readonly int _count;

        public StreamingEventContent(LoadOptions options, int start, int count)
        {
            _options = options;
            _start = start;
            _count = count;
            Headers.ContentType = new MediaTypeHeaderValue("application/x-ndjson");
            if (!String.Equals(options.Compression, "none", StringComparison.Ordinal))
                Headers.ContentEncoding.Add(options.Compression);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return WriteAsync(stream, CancellationToken.None);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            return WriteAsync(stream, cancellationToken);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        private async Task WriteAsync(Stream output, CancellationToken cancellationToken)
        {
            await using Stream? compressor = CreateCompressionStream(output, _options.Compression);
            Stream target = compressor ?? output;
            for (int offset = 0; offset < _count; offset++)
            {
                int index = _start + offset;
                bool discardedCandidate = index % 100 < _options.DiscardPercent;
                int signature = index % _options.SignatureCardinality;
                string signatureKind = discardedCandidate ? "Discarded" : "Active";
                var source = new EventIngestionV3Event
                {
                    Id = $"load-{_options.Seed}-{index:D12}",
                    Type = Event.KnownTypes.Error,
                    Date = DateTimeOffset.UtcNow,
                    Message = _options.Message,
                    ExceptionType = $"Load.{signatureKind}Exception{signature}",
                    StackTrace = $"at Load.{signatureKind}Service{signature}.Run() in /src/Load.cs:line {index % 1000 + 1}"
                };

                await JsonSerializer.SerializeAsync(target, source, EventIngestionJsonContext.Default.EventIngestionV3Event, cancellationToken);
                await target.WriteAsync(_newline, cancellationToken);
            }

            await target.FlushAsync(cancellationToken);
        }

        private static Stream? CreateCompressionStream(Stream output, string compression)
        {
            return compression switch
            {
                "gzip" => new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true),
                "br" => new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true),
                _ => null
            };
        }
    }

    private sealed class LoadTotals
    {
        public long SuccessfulSegments;
        public long FailedSegments;
        public long Received;
        public long Persisted;
        public long Discarded;
        public long Duplicate;
        public long Blocked;
        public long Invalid;
    }

    private sealed record LoadOptions(
        Uri Url,
        string ApiKey,
        int EventCount,
        int Concurrency,
        int SegmentSize,
        int SignatureCardinality,
        int DiscardPercent,
        string Compression,
        string Seed,
        string Message,
        TimeSpan Timeout)
    {
        public static LoadOptions Parse(string[] args)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < args.Length; index += 2)
            {
                if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException("Every option must use '--name value'.");
                values[args[index][2..]] = args[index + 1];
            }

            if (!values.TryGetValue("url", out string? url) || !Uri.TryCreate(url, UriKind.Absolute, out var endpoint))
                throw new ArgumentException("--url must be an absolute V3 ingestion endpoint.");
            string apiKey = values.GetValueOrDefault("api-key") ?? Environment.GetEnvironmentVariable("EXCEPTIONLESS_API_KEY")
                ?? throw new ArgumentException("Set --api-key or EXCEPTIONLESS_API_KEY.");
            int eventCount = GetInt(values, "events", 10000, 1, 10_000_000);
            int concurrency = GetInt(values, "concurrency", 4, 1, 1024);
            int segmentSize = GetInt(values, "segment-size", 100, 1, 10000);
            int cardinality = GetInt(values, "signature-cardinality", 10, 1, 1_000_000);
            int discardPercent = GetInt(values, "discard-percent", 0, 0, 100);
            int messageBytes = GetInt(values, "message-bytes", 64, 0, 4000);
            int timeoutSeconds = GetInt(values, "timeout-seconds", 300, 1, 86400);
            string compression = values.GetValueOrDefault("compression", "none").ToLowerInvariant();
            if (compression is not ("none" or "gzip" or "br"))
                throw new ArgumentException("--compression must be none, gzip, or br.");

            return new LoadOptions(
                endpoint,
                apiKey,
                eventCount,
                concurrency,
                segmentSize,
                cardinality,
                discardPercent,
                compression,
                values.GetValueOrDefault("seed", "default"),
                new string('x', messageBytes),
                TimeSpan.FromSeconds(timeoutSeconds));
        }

        public static void WriteUsage()
        {
            Console.Error.WriteLine("dotnet run --project benchmarks/Exceptionless.Ingestion.Load -- --url <api/v3/events> [--api-key <key>] [--events 10000] [--concurrency 4] [--segment-size 100] [--signature-cardinality 10] [--discard-percent 0] [--compression none|gzip|br] [--seed default]");
        }

        private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int defaultValue, int minimum, int maximum)
        {
            if (!values.TryGetValue(key, out string? raw))
                return defaultValue;
            if (!Int32.TryParse(raw, out int value) || value < minimum || value > maximum)
                throw new ArgumentException($"--{key} must be between {minimum} and {maximum}.");
            return value;
        }
    }
}
