using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Serialization;
using Xunit;

namespace Exceptionless.Tests.Serializer;

public sealed class EventIngestionJsonContextTests
{
    [Fact]
    public async Task DeserializeAsyncEnumerable_MultipleTopLevelValues_ReturnsEveryEvent()
    {
        const string payload = """
            {"id":"event-1","type":"error","stack_trace":"at Example.Run()"}
            {"id":"event-2","type":"log","message":"completed"}
            """;

        var events = await DeserializeAsync(payload);

        Assert.Collection(events,
            first =>
            {
                Assert.Equal("event-1", first.Id);
                Assert.Equal("error", first.Type);
                Assert.Equal("at Example.Run()", first.StackTrace);
            },
            second =>
            {
                Assert.Equal("event-2", second.Id);
                Assert.Equal("log", second.Type);
                Assert.Equal("completed", second.Message);
            });
    }

    [Fact]
    public Task DeserializeAsyncEnumerable_MissingRequiredId_ThrowsJsonException()
    {
        const string payload = """{"type":"log","message":"completed"}""";

        return Assert.ThrowsAsync<JsonException>(() => DeserializeAsync(payload));
    }

    [Fact]
    public async Task DeserializeAsyncEnumerable_UnknownProperty_IgnoresProperty()
    {
        const string payload = """{"id":"event-1","type":"log","future_value":42}""";

        var events = await DeserializeAsync(payload);

        Assert.Single(events);
        Assert.Equal("event-1", events[0].Id);
    }

    [Fact]
    public Task DeserializeAsyncEnumerable_TopLevelArray_ThrowsJsonException()
    {
        const string payload = """[{"id":"event-1","type":"log"}]""";

        return Assert.ThrowsAsync<JsonException>(() => DeserializeAsync(payload));
    }

    [Fact]
    public void Serialize_HtmlSensitiveCharacters_UsesSafeEncoding()
    {
        var value = new EventIngestionV3Event
        {
            Id = "event-1",
            Type = "log",
            Message = "<script>&'"
        };

        string json = JsonSerializer.Serialize(
            value,
            EventIngestionJsonContext.Default.EventIngestionV3Event);

        Assert.Contains("\\u003Cscript\\u003E\\u0026\\u0027", json);
    }

    private static async Task<List<EventIngestionV3Event>> DeserializeAsync(string payload)
    {
        var events = new List<EventIngestionV3Event>();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var reader = PipeReader.Create(stream);

        try
        {
            await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable(
                reader,
                EventIngestionJsonContext.Default.EventIngestionV3Event,
                topLevelValues: true))
            {
                if (item is null)
                {
                    throw new JsonException("Null events are not valid.");
                }

                events.Add(item);
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }

        return events;
    }
}
