using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Serialization;
using Exceptionless.Web.Utility;
using Xunit;

namespace Exceptionless.Tests.Utility;

public sealed class EventIngestionV3StreamReaderTests
{
    private static readonly JsonSerializerOptions _serializerOptions = new(new JsonSerializerOptions().ConfigureExceptionlessDefaults())
    {
        RespectNullableAnnotations = false
    };

    [Fact]
    public async Task ReadAsync_SingleObject_ReturnsEvent()
    {
        // Act
        var records = await ReadAllAsync("""{"message":"hello"}""");

        // Assert
        var record = Assert.Single(records);
        Assert.Equal(0, record.Index);
        Assert.Equal("hello", record.Event?.Message);
    }

    [Fact]
    public async Task ReadAsync_PrettyPrintedObject_ReturnsEvent()
    {
        // Arrange
        const string payload = """
            {
              "id": "one",
              "type": "log",
              "message": "hello",
              "tags": [
                "a",
                "b"
              ]
            }
            """;

        // Act
        var records = await ReadAllAsync(payload);

        // Assert
        var ev = Assert.Single(records).Event;
        Assert.NotNull(ev);
        Assert.Equal("one", ev.Id);
        Assert.Equal(["a", "b"], ev.Tags ?? []);
    }

    [Fact]
    public async Task ReadAsync_NewlineDelimitedObjectsWithBlankLinesAndCrLf_ReturnsEventsInOrder()
    {
        // Arrange
        const string payload = "{\"message\":\"first\"}\r\n\r\n  {\"message\":\"second\"}\n{\"message\":\"third\"}";

        // Act
        var records = await ReadAllAsync(payload);

        // Assert
        Assert.Equal(["first", "second", "third"], records.Select(r => r.Event?.Message));
        Assert.Equal([0, 1, 2], records.Select(r => r.Index));
    }

    [Fact]
    public async Task ReadAsync_ConcatenatedObjectsOnOneLine_ReturnsEachEvent()
    {
        // Act
        var records = await ReadAllAsync("""{"message":"first"}{"message":"second"}""");

        // Assert
        Assert.Equal(["first", "second"], records.Select(r => r.Event?.Message));
    }

    [Fact]
    public async Task ReadAsync_JsonArray_ReturnsEachElement()
    {
        // Arrange
        const string payload = """
            [
              {"message":"first"},
              {"message":"second"}
            ]
            """;

        // Act
        var records = await ReadAllAsync(payload);

        // Assert
        Assert.Equal(["first", "second"], records.Select(r => r.Event?.Message));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n ")]
    [InlineData("[]")]
    [InlineData(" [ \n ] ")]
    public async Task ReadAsync_NoEvents_ReturnsNoRecords(string payload)
    {
        // Act
        var records = await ReadAllAsync(payload);

        // Assert
        Assert.Empty(records);
    }

    [Fact]
    public async Task ReadAsync_ByteOrderMark_IsIgnored()
    {
        // Arrange
        byte[] payload = [.. "﻿"u8, .. """{"message":"hello"}"""u8];

        // Act
        var records = await ReadAllAsync(payload);

        // Assert
        Assert.Equal("hello", Assert.Single(records).Event?.Message);
    }

    [Fact]
    public async Task ReadAsync_NestedDataModels_DeserializeLikeVersionTwoData()
    {
        // Arrange
        const string payload = """
            {"type":"error","error":{"message":"boom","type":"System.InvalidOperationException","stack_trace":[{"name":"Run","declaring_namespace":"Example","declaring_type":"Service","line_number":42}]},"request":{"http_method":"GET","path":"/orders","headers":{"Accept":["application/json"]}},"user":{"identity":"user@example.com","name":"Example User"},"stacking":{"title":"Orders","signature_data":{"area":"orders"}},"data":{"order":{"id":12,"total":10.5}}}
            """;

        // Act
        var ev = Assert.Single(await ReadAllAsync(payload)).Event;

        // Assert
        Assert.NotNull(ev);
        Assert.Equal("System.InvalidOperationException", ev.Error?.Type);
        var frame = Assert.Single(ev.Error!.StackTrace!);
        Assert.Equal("Run", frame.Name);
        Assert.Equal(42, frame.LineNumber);
        Assert.Equal("/orders", ev.Request?.Path);
        Assert.Equal(["application/json"], ev.Request?.Headers?["Accept"] ?? []);
        Assert.Equal("user@example.com", ev.User?.Identity);
        Assert.Equal("orders", ev.Stacking?.SignatureData?["area"]);
        Assert.True(ev.Data?.ContainsKey("order"));
    }

    [Theory]
    [InlineData("\"text\"\n{\"message\":\"after\"}")]
    [InlineData("42\n{\"message\":\"after\"}")]
    [InlineData("null\n{\"message\":\"after\"}")]
    [InlineData("{\"message\":\"x\"}"+"\n[1,2]\n{\"message\":\"after\"}")]
    [InlineData("[1, {\"message\":\"after\"}]")]
    [InlineData("[[{\"message\":\"nested\"}], {\"message\":\"after\"}]")]
    public async Task ReadAsync_NonObjectValue_ReportsInvalidAndContinues(string payload)
    {
        // Act
        var records = (await ReadAllAsync(payload)).Where(r => r.Event?.Message != "x").ToList();

        // Assert
        Assert.Equal(2, records.Count);
        Assert.Equal(EventIngestionV3ErrorCodes.InvalidEvent, records[0].ErrorCode);
        Assert.Null(records[0].Event);
        Assert.Equal("after", records[1].Event?.Message);
    }

    [Fact]
    public async Task ReadAsync_WrongPropertyType_ReportsInvalidAndContinues()
    {
        // Act
        var records = await ReadAllAsync("{\"id\":1,\"message\":\"bad\"}\n{\"id\":\"2\",\"message\":\"good\"}");

        // Assert
        Assert.Equal(EventIngestionV3ErrorCodes.InvalidEvent, records[0].ErrorCode);
        Assert.Contains("$.id", records[0].ErrorMessage);
        Assert.Equal("good", records[1].Event?.Message);
    }

    [Fact]
    public async Task ReadAsync_InvalidJsonLine_ReportsInvalidAndResumesAtNextLine()
    {
        // Arrange
        const string payload = "{\"message\":\"first\"}\n{\"message\": oops}\n{\"message\":\"third\"}";

        // Act
        var records = await ReadAllAsync(payload);

        // Assert
        Assert.Equal(3, records.Count);
        Assert.Equal("first", records[0].Event?.Message);
        Assert.Equal(EventIngestionV3ErrorCodes.InvalidJson, records[1].ErrorCode);
        Assert.Equal("third", records[2].Event?.Message);
        Assert.Equal(2, records[2].Index);
    }

    [Fact]
    public async Task ReadAsync_InvalidPrettyPrintedObject_ResumesAtNextObject()
    {
        // Arrange
        const string payload = """
            {
              "message": "broken",
              "tags": [ "a" "b" ],
              "type": "log"
            }
            {
              "message": "next"
            }
            """;

        // Act
        var records = await ReadAllAsync(payload);

        // Assert
        Assert.Equal(2, records.Count);
        Assert.Equal(EventIngestionV3ErrorCodes.InvalidJson, records[0].ErrorCode);
        Assert.Equal("next", records[1].Event?.Message);
    }

    [Fact]
    public async Task ReadAsync_TruncatedFinalObject_ReportsInvalid()
    {
        // Act
        var records = await ReadAllAsync("{\"message\":\"first\"}\n{\"message\":\"sec");

        // Assert
        Assert.Equal("first", records[0].Event?.Message);
        Assert.Equal(EventIngestionV3ErrorCodes.InvalidJson, records[1].ErrorCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsync_EventLargerThanLimit_ReportsTooLargeAndContinues(bool asArray)
    {
        // Arrange
        string large = $$"""{"message":"{{new string('x', 4096)}}"}""";
        string small = """{"message":"small"}""";
        string payload = asArray ? $"[{large},{small}]" : $"{large}\n{small}";

        // Act
        var records = await ReadAllAsync(payload, maximumEventSize: 1024);

        // Assert
        Assert.Equal(2, records.Count);
        Assert.Equal(EventIngestionV3ErrorCodes.EventTooLarge, records[0].ErrorCode);
        Assert.Equal("small", records[1].Event?.Message);
    }

    [Theory]
    [InlineData("{\"message\":\"first\"}\n{\"message\":\"second\"}\n")]
    [InlineData("[{\"message\":\"first\"} , {\"message\":\"second\"}]")]
    [InlineData("{\n \"message\": \"first\"\n}\n{\"message\":\"second\"}")]
    public async Task ReadAsync_OneByteAtATime_ReturnsSameEvents(string payload)
    {
        // Act
        var records = await ReadAllAsync(Encoding.UTF8.GetBytes(payload), oneByteAtATime: true);

        // Assert
        Assert.Equal(["first", "second"], records.Select(r => r.Event?.Message));
    }

    [Fact]
    public async Task ReadAsync_LargeEventOneByteAtATime_SkipsWithoutFailingTheStream()
    {
        // Arrange
        string payload = $$"""{"message":"{{new string('x', 2048)}}"}""" + "\n" + """{"message":"small"}""";

        // Act
        var records = await ReadAllAsync(Encoding.UTF8.GetBytes(payload), maximumEventSize: 256, oneByteAtATime: true);

        // Assert
        Assert.Equal(EventIngestionV3ErrorCodes.EventTooLarge, records[0].ErrorCode);
        Assert.Equal("small", records[1].Event?.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsync_SingleHugeTokenArrivingInSmallChunks_IsSkippedAndTheStreamContinues(bool asArray)
    {
        // Arrange
        string huge = $$"""{"message":"{{new string('x', 256 * 1024)}}\"quoted\" {not structure}"}""";
        string small = """{"message":"small"}""";
        string payload = asArray ? $"[{huge},{small}]" : $"{huge}\n{small}";

        // Act
        var records = await ReadAllAsync(Encoding.UTF8.GetBytes(payload), maximumEventSize: 1024, chunkSize: 7);

        // Assert
        Assert.Equal(2, records.Count);
        Assert.Equal(EventIngestionV3ErrorCodes.EventTooLarge, records[0].ErrorCode);
        Assert.True(records[0].Size > 256 * 1024);
        Assert.Equal("small", records[1].Event?.Message);
    }

    [Fact]
    public async Task ReadAsync_OversizedTopLevelString_IsSkipped()
    {
        // Arrange
        string payload = $"\"{new string('x', 4096)}\"\n{{\"message\":\"after\"}}";

        // Act
        var records = await ReadAllAsync(Encoding.UTF8.GetBytes(payload), maximumEventSize: 1024, chunkSize: 100);

        // Assert
        Assert.Equal(EventIngestionV3ErrorCodes.EventTooLarge, records[0].ErrorCode);
        Assert.Equal("after", records[1].Event?.Message);
    }

    [Fact]
    public async Task ReadAsync_InvalidPrettyPrintedObjectWithIndentedObjects_ResumesOnlyAtTopLevelObject()
    {
        // Arrange
        const string payload = """
            {
              "message": "broken",
              "frames": [
                {
                  "name": "Run"
                } oops
              ]
            }
            {"message":"next"}
            """;

        // Act
        var records = await ReadAllAsync(payload);

        // Assert
        Assert.Equal(2, records.Count);
        Assert.Equal(EventIngestionV3ErrorCodes.InvalidJson, records[0].ErrorCode);
        Assert.Equal("next", records[1].Event?.Message);
    }

    [Theory]
    [InlineData("[{\"message\":\"a\"} {\"message\":\"b\"}]")]
    [InlineData("[{\"message\":\"a\"},]")]
    [InlineData("[{\"message\":\"a\"}")]
    [InlineData("[{\"message\":\"a\"}] {\"message\":\"b\"}")]
    [InlineData("[{\"message\": oops}]")]
    public Task ReadAsync_MalformedArray_Throws(string payload)
    {
        // Act & Assert
        return Assert.ThrowsAnyAsync<JsonException>(() => ReadAllAsync(payload));
    }

    private static Task<List<EventIngestionV3StreamRecord>> ReadAllAsync(string payload, long maximumEventSize = 64 * 1024)
    {
        return ReadAllAsync(Encoding.UTF8.GetBytes(payload), maximumEventSize);
    }

    private static async Task<List<EventIngestionV3StreamRecord>> ReadAllAsync(byte[] payload, long maximumEventSize = 64 * 1024, bool oneByteAtATime = false, int? chunkSize = null)
    {
        Stream stream = oneByteAtATime || chunkSize.HasValue ? new ChunkedStream(payload, chunkSize ?? 1) : new MemoryStream(payload);
        var pipeReader = PipeReader.Create(stream);
        var reader = new EventIngestionV3StreamReader(pipeReader, maximumEventSize, _serializerOptions);
        var records = new List<EventIngestionV3StreamRecord>();
        try
        {
            while (await reader.ReadAsync(TestContext.Current.CancellationToken) is { } record)
            {
                records.Add(record);
            }
        }
        finally
        {
            await pipeReader.CompleteAsync();
        }

        return records;
    }

    private sealed class ChunkedStream(byte[] payload, int chunkSize) : MemoryStream(payload)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return base.ReadAsync(buffer.Length > chunkSize ? buffer[..chunkSize] : buffer, cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return base.Read(buffer, offset, Math.Min(count, chunkSize));
        }
    }
}
