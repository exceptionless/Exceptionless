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
    private byte[] _v3StreamPayload = null!;
    private JsonSerializerOptions _v2JsonOptions = null!;

    [Params(1, 100, 1000)]
    public int EventCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _v2JsonOptions = new JsonSerializerOptions().ConfigureExceptionlessDefaults();
        _v2JsonOptions.RespectNullableAnnotations = false;
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
                Type = "error",
                Date = DateTimeOffset.UnixEpoch.AddSeconds(index),
                Message = message,
                ExceptionType = exceptionType,
                StackTrace = stackTrace
            };
        }

        _v2Payload = EventCount == 1
            ? JsonSerializer.SerializeToUtf8Bytes(v2Events[0])
            : JsonSerializer.SerializeToUtf8Bytes(v2Events);

        using var stream = new MemoryStream(_v2Payload.Length);
        for (int index = 0; index < v3Events.Length; index++)
        {
            JsonSerializer.Serialize(
                stream,
                v3Events[index],
                EventIngestionJsonContext.Default.EventIngestionV3Event);
            stream.WriteByte((byte)'\n');
        }

        _v3StreamPayload = stream.ToArray();
    }

    [Benchmark(Baseline = true)]
    public int DeserializeV2Payload()
    {
        string input = Encoding.UTF8.GetString(_v2Payload);
        return input.GetJsonType() switch
        {
            JsonType.Object => JsonSerializer.Deserialize<PersistentEvent>(input, _v2JsonOptions) is null ? 0 : 1,
            JsonType.Array => JsonSerializer.Deserialize<PersistentEvent[]>(input, _v2JsonOptions)?.Length ?? 0,
            _ => 0
        };
    }

    [Benchmark]
    public Task<int> FrameAndRouteV3NdjsonAsync() => ReadV3NdjsonAsync(materializeSurvivors: false);

    [Benchmark]
    public Task<int> FrameRouteAndMaterializeV3SurvivorsAsync() => ReadV3NdjsonAsync(materializeSurvivors: true);

    private async Task<int> ReadV3NdjsonAsync(bool materializeSurvivors)
    {
        using var stream = new MemoryStream(_v3StreamPayload, writable: false);
        var reader = PipeReader.Create(stream);
        int count = 0;
        try
        {
            while (await EventIngestionV3StreamReader.ReadAsync(reader, MaximumEventSize, CancellationToken.None) is { } record)
            {
                try
                {
                    EventIngestionV3Event parsed = materializeSurvivors
                        ? record.BufferedRecord.Materialize()
                        : record.Event;
                    if (!String.IsNullOrEmpty(parsed.Id))
                        count++;
                }
                finally
                {
                    record.BufferedRecord.Dispose();
                }
            }
        }
        finally
        {
            await reader.CompleteAsync();
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
    private JsonSerializerOptions _v2JsonOptions = null!;

    [Params(16 * 1024, 128 * 1024)]
    public int StackTraceLength { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        string stackTrace = new('x', StackTraceLength);
        _v2JsonOptions = new JsonSerializerOptions().ConfigureExceptionlessDefaults();
        _v2JsonOptions.RespectNullableAnnotations = false;
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
        _v3Payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            id = "large-stack-event",
            type = "error",
            message = "Operation failed",
            exception_type = "Example.Exception",
            stack_trace = stackTrace
        });
    }

    [Benchmark(Baseline = true)]
    public PersistentEvent? DeserializeV2Payload()
    {
        string input = Encoding.UTF8.GetString(_v2Payload);
        return JsonSerializer.Deserialize<PersistentEvent>(input, _v2JsonOptions);
    }

    [Benchmark]
    public Task<int> FrameAndRouteV3NdjsonAsync() => ReadV3NdjsonAsync(materializeSurvivor: false);

    [Benchmark]
    public Task<int> FrameRouteAndMaterializeV3SurvivorAsync() => ReadV3NdjsonAsync(materializeSurvivor: true);

    private async Task<int> ReadV3NdjsonAsync(bool materializeSurvivor)
    {
        using var stream = new MemoryStream(_v3Payload, writable: false);
        var reader = PipeReader.Create(stream);
        try
        {
            EventIngestionV3StreamRecord? record = await EventIngestionV3StreamReader.ReadAsync(
                reader,
                MaximumEventSize,
                CancellationToken.None);
            if (record is null)
                return 0;

            try
            {
                EventIngestionV3Event parsed = materializeSurvivor
                    ? record.Value.BufferedRecord.Materialize()
                    : record.Value.Event;
                return parsed.StackTrace?.Length ?? 0;
            }
            finally
            {
                record.Value.BufferedRecord.Dispose();
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }
    }
}
