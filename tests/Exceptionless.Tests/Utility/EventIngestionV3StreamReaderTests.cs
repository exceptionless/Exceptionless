using System.IO.Pipelines;
using System.Text;
using Exceptionless.Web.Utility;
using Xunit;

namespace Exceptionless.Tests.Utility;

public sealed class EventIngestionV3StreamReaderTests
{
    [Fact]
    public async Task ReadAsync_NewlineDelimitedObjects_ReturnsOneBoundedRecordAtATime()
    {
        const string payload = """
              {"id":"first","type":"log"}

            {"id":"second","type":"log"}
            """;
        PipeReader reader = PipeReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(payload)));

        EventIngestionV3StreamRecord? first = await EventIngestionV3StreamReader.ReadAsync(reader, 1024, TestContext.Current.CancellationToken);
        EventIngestionV3StreamRecord? second = await EventIngestionV3StreamReader.ReadAsync(reader, 1024, TestContext.Current.CancellationToken);
        EventIngestionV3StreamRecord? end = await EventIngestionV3StreamReader.ReadAsync(reader, 1024, TestContext.Current.CancellationToken);

        Assert.Equal("first", first?.Event?.Id);
        Assert.Equal("second", second?.Event?.Id);
        Assert.Null(end);
        first?.BufferedRecord.Dispose();
        second?.BufferedRecord.Dispose();
        await reader.CompleteAsync();
    }

    [Fact]
    public async Task ReadAsync_AdjacentObjectsOnOneLine_RejectsAmbiguousFraming()
    {
        const string payload = """{"id":"first","type":"log"}{"id":"second","type":"log"}""";
        PipeReader reader = PipeReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(payload)));

        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(async () =>
            await EventIngestionV3StreamReader.ReadAsync(reader, 1024, TestContext.Current.CancellationToken));

        await reader.CompleteAsync();
    }

    [Fact]
    public async Task ReadAsync_RecordPrefixOverLimit_RejectsBeforeDeserialization()
    {
        string payload = $$"""{"id":"large","type":"log","unknown":"{{new string('x', 4096)}}"}""";
        PipeReader reader = PipeReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(payload)));

        await Assert.ThrowsAsync<EventIngestionV3RecordTooLargeException>(async () =>
            await EventIngestionV3StreamReader.ReadAsync(reader, 128, TestContext.Current.CancellationToken));

        await reader.CompleteAsync();
    }

    [Fact]
    public async Task ReadAsync_TopLevelArray_RejectsWithoutBufferingWholeValue()
    {
        string payload = $"[{{\"id\":\"first\",\"type\":\"log\"}},\"{new string('x', 4096)}\"]";
        PipeReader reader = PipeReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(payload)));

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(async () =>
            await EventIngestionV3StreamReader.ReadAsync(reader, 128, TestContext.Current.CancellationToken));

        await reader.CompleteAsync();
    }

    [Fact]
    public async Task ReadAsync_MissingRequiredEventFields_DefersToPerEventValidation()
    {
        PipeReader reader = PipeReader.Create(new MemoryStream("{}"u8.ToArray()));

        EventIngestionV3StreamRecord? record = await EventIngestionV3StreamReader.ReadAsync(
            reader,
            128,
            TestContext.Current.CancellationToken);

        Assert.True(record.HasValue);
        Assert.NotNull(record.Value.Event);
        Assert.Null(record.Value.Event.Id);
        Assert.Null(record.Value.Event.Type);
        record.Value.BufferedRecord.Dispose();
        await reader.CompleteAsync();
    }

    [Fact]
    public async Task ReadAsync_LargeRecordArrivingInSmallFragments_IsReadOnce()
    {
        string payload = "{\"id\":\"fragmented\",\"type\":\"log\",\"data\":{\"value\":\""
            + new string('x', 512 * 1024)
            + "\"}}";
        await using var stream = new FragmentedReadStream(Encoding.UTF8.GetBytes(payload), 257);
        PipeReader reader = PipeReader.Create(stream, new StreamPipeReaderOptions(
            bufferSize: 512,
            minimumReadSize: 1,
            leaveOpen: true));

        EventIngestionV3StreamRecord? record = await EventIngestionV3StreamReader.ReadAsync(
            reader,
            1024 * 1024,
            TestContext.Current.CancellationToken);

        Assert.Equal("fragmented", record?.Event?.Id);
        Assert.True(stream.ReadCount > 1000);
        record?.BufferedRecord.Dispose();
        await reader.CompleteAsync();
    }

    [Fact]
    public async Task ReadAsync_RoutingProjection_RetainsPooledBytesUntilExplicitlyMaterialized()
    {
        const string payload = """
            {"id":"projected","type":"log","source":"Example.Service","stacking":{"title":"Orders failed","signature_data":{"Type":"orders"}},"data":{"large":"context"},"request":{"path":"/orders"}}
            """;
        using var pool = new TrackingMemoryPool();
        PipeReader reader = PipeReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(payload)));

        EventIngestionV3StreamRecord? record = await EventIngestionV3StreamReader.ReadAsync(
            reader,
            1024,
            TestContext.Current.CancellationToken,
            pool);

        Assert.True(record.HasValue);
        Assert.Equal(1, pool.OutstandingRentals);
        Assert.False(record.Value.BufferedRecord.IsMaterialized);
        Assert.Equal("projected", record.Value.Event.Id);
        Assert.Equal("orders", record.Value.Event.Stacking?.SignatureData["Type"]);
        Assert.Null(record.Value.Event.Stacking?.Title);
        Assert.Null(record.Value.Event.Data);
        Assert.Null(record.Value.Event.Request);

        var materialized = record.Value.BufferedRecord.Materialize();
        Assert.True(record.Value.BufferedRecord.IsMaterialized);
        record.Value.BufferedRecord.Dispose();
        Assert.Equal(0, pool.OutstandingRentals);
        Assert.Equal("context", materialized.Data?.GetProperty("large").GetString());
        Assert.Equal("/orders", materialized.Request?.Path);
        Assert.Equal("Orders failed", materialized.Stacking?.Title);
        Assert.Same(record.Value.Event.Id, materialized.Id);
        Assert.Same(record.Value.Event.Type, materialized.Type);
        Assert.Same(record.Value.Event.Source, materialized.Source);
        Assert.Same(record.Value.Event.Stacking?.SignatureData, materialized.Stacking?.SignatureData);
        await reader.CompleteAsync();
    }

    [Fact]
    public async Task Materialize_LargeRawStack_ReusesRoutingStringInsteadOfAllocatingItTwice()
    {
        string stackTrace = new('x', 128 * 1024);
        string payload = $$"""{"id":"large-stack","type":"error","exception_type":"Example.Exception","stack_trace":"{{stackTrace}}","message":"failed"}""";
        PipeReader reader = PipeReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(payload)));

        EventIngestionV3StreamRecord? record = await EventIngestionV3StreamReader.ReadAsync(
            reader,
            256 * 1024,
            TestContext.Current.CancellationToken);

        Assert.True(record.HasValue);
        var materialized = record.Value.BufferedRecord.Materialize();
        Assert.Same(record.Value.Event.Id, materialized.Id);
        Assert.Same(record.Value.Event.Type, materialized.Type);
        Assert.Same(record.Value.Event.ExceptionType, materialized.ExceptionType);
        Assert.Same(record.Value.Event.StackTrace, materialized.StackTrace);
        Assert.Equal("failed", materialized.Message);

        record.Value.BufferedRecord.Dispose();
        await reader.CompleteAsync();
    }

    private sealed class FragmentedReadStream(byte[] value, int maximumReadSize) : MemoryStream(value)
    {
        public int ReadCount { get; private set; }

        public override int Read(Span<byte> buffer)
        {
            ReadCount++;
            return base.Read(buffer[..Math.Min(buffer.Length, maximumReadSize)]);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return base.ReadAsync(buffer[..Math.Min(buffer.Length, maximumReadSize)], cancellationToken);
        }
    }
}
