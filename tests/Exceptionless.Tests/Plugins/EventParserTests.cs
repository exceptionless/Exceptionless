using System.Text.Json;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventParser;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Plugins;

public sealed class EventParserTests : TestWithServices
{
    private readonly EventParserPluginManager _parser;
    private readonly ITextSerializer _serializer;

    public EventParserTests(ITestOutputHelper output) : base(output)
    {
        _parser = GetService<EventParserPluginManager>();
        _serializer = GetService<ITextSerializer>();
    }

    public static IEnumerable<object?[]> EventData => new[] {
        [" \t", 0, null, Event.KnownTypes.Log], ["simple string", 1, new [] { "simple string" }, Event.KnownTypes.Log],
        [" \r\nsimple string", 1, new [] { "simple string" }, Event.KnownTypes.Log], ["{simple string", 1, new [] { "{simple string" }, Event.KnownTypes.Log
        ],
        ["{simple string,simple string}", 1, new [] { "{simple string,simple string}" }, Event.KnownTypes.Log],
        ["{ \"name\": \"value\" }", 1, new string?[] { null }, Event.KnownTypes.Log],
        ["{ \"message\": \"simple string\" }", 1, new [] { "simple string" }, Event.KnownTypes.Log],
        ["{ \"message\": \"simple string\", \"data\": { \"" + Event.KnownDataKeys.Error + "\": {} } }", 1, new [] { "simple string" }, Event.KnownTypes.Error
        ],
        ["[simple string", 1, new [] { "[simple string" }, Event.KnownTypes.Log], ["[simple string,simple string]", 1, new [] { "[simple string,simple string]" }, Event.KnownTypes.Log
        ],
            new object?[] { "simple string\r\nsimple string", 2, new [] { "simple string", "simple string" }, Event.KnownTypes.Log }
        };

    [Theory]
    [MemberData(nameof(EventData))]
    public void ParseEvents(string input, int expectedEvents, string?[]? expectedMessage, string expectedType)
    {
        var events = _parser.ParseEvents(input, 2, "exceptionless/2.0.0.0");
        Assert.Equal(expectedEvents, events.Count);
        for (int index = 0; index < events.Count; index++)
        {
            var ev = events[index];
            Assert.Equal(expectedMessage?[index], ev.Message);
            Assert.Equal(expectedType, ev.Type);
            Assert.NotEqual(DateTimeOffset.MinValue, ev.Date);
        }
    }

    [Theory]
    [MemberData(nameof(Events))]
    public void VerifyEventParserSerialization(string eventsFilePath)
    {
        string json = File.ReadAllText(eventsFilePath);

        var events = _parser.ParseEvents(json, 2, "exceptionless/2.0.0.0");
        Assert.Single(events);

        // Verify parsed event can round-trip through STJ serialization
        string serialized = _serializer.SerializeToString(events.First());
        Assert.NotNull(serialized);
        var roundTripped = _serializer.Deserialize<Event>(serialized);
        Assert.NotNull(roundTripped);
        Assert.Equal(events.First().Type, roundTripped.Type);
        Assert.Equal(events.First().Message, roundTripped.Message);
    }

    [Theory]
    [MemberData(nameof(Events))]
    public void CanDeserializeEvents(string eventsFilePath)
    {
        string json = File.ReadAllText(eventsFilePath);

        var ev = _serializer.Deserialize<PersistentEvent>(json);
        Assert.NotNull(ev);
    }

    public static TheoryData<string> Events
    {
        get
        {
            var result = new List<string>();
            foreach (string file in Directory.GetFiles(Path.Combine("..", "..", "..", "Search", "Data"), "event*.json", SearchOption.AllDirectories))
                if (!file.EndsWith("summary.json"))
                    result.Add(Path.GetFullPath(file));

            return new TheoryData<string>(result);
        }
    }
}
