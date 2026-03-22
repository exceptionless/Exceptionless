using System.Text.Json;
using System.Text.Json.Nodes;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventParser;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Plugins;

public sealed class EventParserTests : TestWithServices
{
    private readonly EventParserPluginManager _parser;
    private readonly ITextSerializer _serializer;
    private readonly JsonSerializerOptions _jsonOptions;

    public EventParserTests(ITestOutputHelper output) : base(output)
    {
        _parser = GetService<EventParserPluginManager>();
        _serializer = GetService<ITextSerializer>();
        _jsonOptions = GetService<JsonSerializerOptions>();
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
        var ev = events.First();

        // Verify structural equivalence: parse → serialize should produce
        // content equivalent to the original file (ignoring nulls and empty collections
        // that STJ's WhenWritingNull and EmptyCollectionModifier skip).
        string expectedContent = File.ReadAllText(eventsFilePath);
        string actualContent = JsonSerializer.Serialize(ev, _jsonOptions);
        AssertJsonEquivalent(expectedContent, actualContent);
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

    /// <summary>
    /// Compares two JSON strings semantically, ignoring null properties and empty collections
    /// that differ between Newtonsoft and STJ serialization.
    /// </summary>
    private static void AssertJsonEquivalent(string expectedJson, string actualJson)
    {
        var expected = JsonNode.Parse(expectedJson);
        var actual = JsonNode.Parse(actualJson);
        RemoveNullAndEmptyProperties(expected);
        RemoveNullAndEmptyProperties(actual);
        Assert.True(JsonNode.DeepEquals(expected, actual),
            $"Expected:\n{expected?.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}\n\nActual:\n{actual?.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}");
    }

    private static void RemoveNullAndEmptyProperties(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var keysToRemove = new List<string>();
            foreach (var prop in obj)
            {
                if (prop.Value is null)
                    keysToRemove.Add(prop.Key);
                else if (prop.Value is JsonArray arr && arr.Count == 0)
                    keysToRemove.Add(prop.Key);
                else
                    RemoveNullAndEmptyProperties(prop.Value);
            }

            foreach (string key in keysToRemove)
                obj.Remove(key);
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
                RemoveNullAndEmptyProperties(item);
        }
    }
}
