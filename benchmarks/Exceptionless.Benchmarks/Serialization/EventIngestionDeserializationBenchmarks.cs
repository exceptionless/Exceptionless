using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Serialization;

namespace Exceptionless.Benchmarks.Serialization;

[MemoryDiagnoser]
public class EventIngestionDeserializationBenchmarks
{
    private byte[] _arrayPayload = null!;
    private byte[] _streamPayload = null!;

    [Params(1, 100, 1000)]
    public int EventCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var events = new EventIngestionV3Event[EventCount];
        for (int index = 0; index < events.Length; index++)
        {
            events[index] = new EventIngestionV3Event
            {
                Id = $"01J0000000000000000000{index:D4}",
                Type = "error",
                Date = DateTimeOffset.UnixEpoch.AddSeconds(index),
                Message = "Operation failed",
                ExceptionType = "System.InvalidOperationException",
                StackTrace = "   at Example.Service.Run() in Service.cs:line 42"
            };
        }

        _arrayPayload = JsonSerializer.SerializeToUtf8Bytes(
            events,
            EventIngestionJsonContext.Default.EventIngestionV3EventArray);

        using var stream = new MemoryStream(_arrayPayload.Length);
        for (int index = 0; index < events.Length; index++)
        {
            JsonSerializer.Serialize(
                stream,
                events[index],
                EventIngestionJsonContext.Default.EventIngestionV3Event);
            stream.WriteByte((byte)'\n');
        }

        _streamPayload = stream.ToArray();
    }

    [Benchmark(Baseline = true)]
    public int DeserializeArray()
    {
        var events = JsonSerializer.Deserialize(
            _arrayPayload,
            EventIngestionJsonContext.Default.EventIngestionV3EventArray);

        return events?.Length ?? 0;
    }

    [Benchmark]
    public async Task<int> DeserializeTopLevelValuesAsync()
    {
        using var stream = new MemoryStream(_streamPayload, writable: false);
        var reader = PipeReader.Create(stream);
        int count = 0;

        await foreach (var _ in JsonSerializer.DeserializeAsyncEnumerable(
            reader,
            EventIngestionJsonContext.Default.EventIngestionV3Event,
            topLevelValues: true))
        {
            count++;
        }

        await reader.CompleteAsync();
        return count;
    }
}
