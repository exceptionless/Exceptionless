using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Serialization;
using Exceptionless.Web.Utility;

namespace Exceptionless.Benchmarks.Serialization;

[MemoryDiagnoser]
public class EventIngestionDeserializationBenchmarks
{
    private const int MaximumEventSize = 512 * 1024;
    private byte[] _v2Payload = null!;
    private byte[] _v3NdjsonPayload = null!;
    private byte[] _v3ArrayPayload = null!;
    private JsonSerializerOptions _readOptions = null!;

    [Params(1, 100, 1000)]
    public int EventCount { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        // Payloads are written with the app options, like a client following the V3 contract. The
        // read options match what the V2 parser and EventIngestionV3Processor use.
        var jsonOptions = new JsonSerializerOptions().ConfigureExceptionlessDefaults();
        _readOptions = new JsonSerializerOptions(jsonOptions) { RespectNullableAnnotations = false };

        var v2Events = new V2BenchmarkEvent[EventCount];
        var v3Events = new EventIngestionV3Event[EventCount];
        for (int index = 0; index < v3Events.Length; index++)
        {
            const string message = "Operation failed";
            const string exceptionType = "System.InvalidOperationException";
            const string stackTrace = "   at Example.Service.Run() in Service.cs:line 42";
            v2Events[index] = new V2BenchmarkEvent(
                Event.KnownTypes.Error,
                DateTimeOffset.UnixEpoch.AddSeconds(index),
                message,
                new V2BenchmarkData(new V2BenchmarkError(exceptionType, message, stackTrace)));
            v3Events[index] = new EventIngestionV3Event
            {
                Id = $"01J0000000000000000000{index:D4}",
                Type = Event.KnownTypes.Error,
                Date = DateTimeOffset.UnixEpoch.AddSeconds(index),
                Message = message,
                ExceptionType = exceptionType,
                StackTrace = stackTrace
            };
        }

        _v2Payload = EventCount == 1
            ? JsonSerializer.SerializeToUtf8Bytes(v2Events[0])
            : JsonSerializer.SerializeToUtf8Bytes(v2Events);

        using var ndjson = new MemoryStream();
        foreach (var v3Event in v3Events)
        {
            JsonSerializer.Serialize(ndjson, v3Event, jsonOptions);
            ndjson.WriteByte((byte)'\n');
        }

        _v3NdjsonPayload = ndjson.ToArray();
        _v3ArrayPayload = JsonSerializer.SerializeToUtf8Bytes(v3Events, jsonOptions);

        // Fail fast if a payload stops matching the V3 contract, so the benchmarks never time the
        // reader's invalid-event path.
        if (await ReadV3Async(_v3NdjsonPayload) != EventCount || await ReadV3Async(_v3ArrayPayload) != EventCount)
            throw new InvalidOperationException("The V3 benchmark payloads must contain only valid events.");
    }

    [Benchmark(Baseline = true)]
    public int DeserializeV2Payload()
    {
        string input = Encoding.UTF8.GetString(_v2Payload);
        return input.GetJsonType() switch
        {
            JsonType.Object => JsonSerializer.Deserialize<PersistentEvent>(input, _readOptions) is null ? 0 : 1,
            JsonType.Array => JsonSerializer.Deserialize<PersistentEvent[]>(input, _readOptions)?.Length ?? 0,
            _ => 0
        };
    }

    [Benchmark]
    public Task<int> ReadV3NdjsonAsync() => ReadV3Async(_v3NdjsonPayload);

    [Benchmark]
    public Task<int> ReadV3JsonArrayAsync() => ReadV3Async(_v3ArrayPayload);

    private async Task<int> ReadV3Async(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        var pipeReader = PipeReader.Create(stream);
        var reader = new EventIngestionV3StreamReader(pipeReader, MaximumEventSize, _readOptions);
        int count = 0;
        try
        {
            while (await reader.ReadAsync(CancellationToken.None) is { } record)
            {
                if (record.Event is not null)
                    count++;
            }
        }
        finally
        {
            await pipeReader.CompleteAsync();
        }

        return count;
    }

    private sealed record V2BenchmarkEvent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("date")] DateTimeOffset Date,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("data")] V2BenchmarkData Data);

    private sealed record V2BenchmarkData(
        [property: JsonPropertyName("@simple_error")] V2BenchmarkError SimpleError);

    private sealed record V2BenchmarkError(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("stack_trace")] string StackTrace);
}

/// <summary>
/// Keeps large raw stacks visible in allocation results. One accepted error is enough to expose
/// duplicate LOH strings without multiplying the benchmark process's retained payload by a batch.
/// </summary>
[MemoryDiagnoser]
public class LargeStackEventIngestionDeserializationBenchmarks
{
    private const int MaximumEventSize = 512 * 1024;
    private byte[] _v2Payload = null!;
    private byte[] _v3Payload = null!;
    private JsonSerializerOptions _readOptions = null!;

    [Params(16 * 1024, 128 * 1024)]
    public int StackTraceLength { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        string stackTrace = new('x', StackTraceLength);
        var jsonOptions = new JsonSerializerOptions().ConfigureExceptionlessDefaults();
        _readOptions = new JsonSerializerOptions(jsonOptions) { RespectNullableAnnotations = false };
        _v2Payload = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?>
        {
            ["type"] = "error",
            ["message"] = "Operation failed",
            ["data"] = new Dictionary<string, object?>
            {
                ["@simple_error"] = new
                {
                    type = "Example.Exception",
                    message = "Operation failed",
                    stack_trace = stackTrace
                }
            }
        });
        _v3Payload = JsonSerializer.SerializeToUtf8Bytes(
            new EventIngestionV3Event
            {
                Id = "large-stack-event",
                Type = Event.KnownTypes.Error,
                Message = "Operation failed",
                ExceptionType = "Example.Exception",
                StackTrace = stackTrace
            },
            jsonOptions);

        if (await ReadV3Async(_v3Payload) != StackTraceLength)
            throw new InvalidOperationException("The V3 benchmark payload must contain one valid event.");
    }

    [Benchmark(Baseline = true)]
    public PersistentEvent? DeserializeV2Payload()
    {
        string input = Encoding.UTF8.GetString(_v2Payload);
        return JsonSerializer.Deserialize<PersistentEvent>(input, _readOptions);
    }

    [Benchmark]
    public Task<int> ReadV3Async() => ReadV3Async(_v3Payload);

    private async Task<int> ReadV3Async(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        var pipeReader = PipeReader.Create(stream);
        var reader = new EventIngestionV3StreamReader(pipeReader, MaximumEventSize, _readOptions);
        try
        {
            return await reader.ReadAsync(CancellationToken.None) is { Event: { } parsed }
                ? parsed.StackTrace?.Length ?? 0
                : 0;
        }
        finally
        {
            await pipeReader.CompleteAsync();
        }
    }
}
