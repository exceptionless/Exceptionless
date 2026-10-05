using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventParser;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

public sealed class EventEnvironmentTests : TestWithServices
{
    public EventEnvironmentTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData("environment")]
    [InlineData("Environment")]
    [InlineData("ENVIRONMENT")]
    public void Deserialize_DeploymentEnvironment_TrimsNameAndPreservesCasingAndRuntimeMetadata(string property)
    {
        var serializer = GetService<ITextSerializer>();
        var ev = serializer.Deserialize<Event>("""{"PROPERTY":" Production ","data":{"@environment":{"machine_name":"worker-1"},"environment":{"custom":true}}}""".Replace("PROPERTY", property));

        Assert.NotNull(ev);
        Assert.Equal("Production", ev.Environment);
        Assert.Equal("worker-1", ev.GetEnvironmentInfo(serializer, _logger)?.MachineName);
        Assert.NotNull(ev.Data?["environment"]);
        string json = serializer.SerializeToString(ev)!;
        Assert.Contains("\"environment\":\"Production\"", json);
        Assert.Equal("Production", serializer.Deserialize<Event>(json)?.Environment);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("{\"name\":\"production\"}")]
    [InlineData("[\"production\"]")]
    [InlineData("\"   \"")]
    [InlineData("\"production\\ninvalid\"")]
    public void ParseEvents_InvalidEnvironment_PreservesSubmissionBatch(string environment)
    {
        var parser = GetService<JsonEventParserPlugin>();
        var events = parser.ParseEvents($$"""[{"type":"error","message":"first","environment":{{environment}}},{"type":"log","message":"second","environment":"staging"}]""", 2, null);

        Assert.NotNull(events);
        Assert.Equal(2, events.Count);
        Assert.Null(events[0].Environment);
        Assert.Equal("first", events[0].Message);
        Assert.Equal("staging", events[1].Environment);
    }

    [Fact]
    public void Serialize_UnspecifiedEnvironment_OmitsProperty()
    {
        var serializer = GetService<ITextSerializer>();
        Assert.DoesNotContain("environment", serializer.SerializeToString(new Event())!);
        Assert.Null(serializer.Deserialize<Event>("{}")?.Environment);
        Assert.Null(new Event { Environment = new string('x', 65) }.Environment);
        Assert.Equal(new string('X', 64), new Event { Environment = new string('X', 64) }.Environment);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("{\"region\":\"west\"}")]
    [InlineData("[\"production\",\"staging\"]")]
    public void Deserialize_LegacyRootEnvironment_PreservesCustomDataRegardlessOfPropertyOrder(string environment)
    {
        var serializer = GetService<ITextSerializer>();
        using var expected = JsonDocument.Parse(environment);

        foreach (var (environmentFirst, duplicateKey) in new[] { (true, false), (false, false), (true, true), (false, true) })
        {
            string environmentProperty = $"\"environment\":{environment}";
            string dataProperty = duplicateKey
                ? "\"data\":{\"environment\":\"nested\",\"kept\":true}"
                : "\"data\":{\"kept\":true}";
            string json = environmentFirst
                ? $"{{{environmentProperty},{dataProperty}}}"
                : $"{{{dataProperty},{environmentProperty}}}";
            var ev = serializer.Deserialize<Event>(json);

            Assert.NotNull(ev);
            Assert.Null(ev.Environment);
            using var result = JsonDocument.Parse(serializer.SerializeToString(ev)!);
            var data = result.RootElement.GetProperty("data");
            Assert.True(data.GetProperty("kept").GetBoolean());
            Assert.True(JsonElement.DeepEquals(expected.RootElement, data.GetProperty(duplicateKey ? "environment1" : "environment")));
            if (duplicateKey)
            {
                Assert.Equal("nested", data.GetProperty("environment").GetString());
            }
            Assert.False(result.RootElement.TryGetProperty("environment", out _));
        }
    }

    [Fact]
    public void Equals_DifferentEnvironments_DistinguishesEvents()
    {
        Assert.NotEqual(new Event { Environment = "production", Data = null }, new Event { Environment = "staging", Data = null });
        Assert.Equal(new Event { Environment = " Production ", Data = null }, new Event { Environment = "Production", Data = null });
        Assert.NotEqual(new Event { Environment = "Production", Data = null }, new Event { Environment = "production", Data = null });
    }

    [Theory]
    [InlineData("Environment")]
    [InlineData("ENVIRONMENT")]
    [InlineData("environment")]
    public void ParseEvents_LegacyEnvironmentKey_PreservesSubmittedCasing(string propertyName)
    {
        var parser = GetService<JsonEventParserPlugin>();
        var serializer = GetService<ITextSerializer>();

        var ev = Assert.Single(Assert.IsType<List<PersistentEvent>>(parser.ParseEvents($$"""{"{{propertyName}}":"west"}""", 2, null)));

        Assert.Equal("west", ev.Environment);
        using var serialized = JsonDocument.Parse(serializer.SerializeToString(ev)!);
        var property = Assert.Single(serialized.RootElement.GetProperty("data").EnumerateObject());
        Assert.Equal(propertyName, property.Name);
        Assert.Equal("west", property.Value.GetString());
    }

    [Fact]
    public void ParseEvents_CaseDistinctEnvironmentKeys_PreservesAllLegacyValues()
    {
        var parser = GetService<JsonEventParserPlugin>();
        var serializer = GetService<ITextSerializer>();
        const string json = """{"Environment":"west","environment":"east","ENVIRONMENT":{"region":"north"},"data":{"environment":"nested"}}""";

        var ev = Assert.Single(Assert.IsType<List<PersistentEvent>>(parser.ParseEvents(json, 2, null)));

        Assert.Null(ev.Environment);
        using var serialized = JsonDocument.Parse(serializer.SerializeToString(ev)!);
        var data = serialized.RootElement.GetProperty("data");
        Assert.Equal(4, data.EnumerateObject().Count());
        Assert.Equal("nested", data.GetProperty("environment").GetString());
        Assert.Equal("west", data.GetProperty("Environment1").GetString());
        Assert.Equal("east", data.GetProperty("environment2").GetString());
        Assert.Equal("north", data.GetProperty("ENVIRONMENT3").GetProperty("region").GetString());
    }

    [Theory]
    [InlineData("west", "west")]
    [InlineData(" Production ", "Production")]
    [InlineData("   ", null)]
    [InlineData("bad\nenvironment", null)]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklm", null)]
    public void ParseEvents_LegacyStringEnvironment_PreservesOriginalDataAcrossRoundTrips(string environment, string? expected)
    {
        var parser = GetService<JsonEventParserPlugin>();
        var serializer = GetService<ITextSerializer>();

        foreach (var (environmentFirst, duplicateKey) in new[] { (true, false), (false, false), (true, true), (false, true) })
        {
            string environmentProperty = $"\"environment\":{JsonSerializer.Serialize(environment)}";
            string dataProperty = duplicateKey
                ? "\"data\":{\"environment\":\"nested\",\"kept\":true}"
                : "\"data\":{\"kept\":true}";
            string json = environmentFirst
                ? $"{{{environmentProperty},{dataProperty}}}"
                : $"{{{dataProperty},{environmentProperty}}}";

            var ev = Assert.Single(Assert.IsType<List<PersistentEvent>>(parser.ParseEvents(json, 2, null)));
            Assert.Equal(expected, ev.Environment);
            Assert.Equal(environment, ev.Data![duplicateKey ? "environment1" : "environment"]);
            if (duplicateKey)
                Assert.Equal("nested", ev.Data["environment"]);

            string serialized = serializer.SerializeToString(ev)!;
            for (int i = 0; i < 3; i++)
            {
                ev = serializer.Deserialize<PersistentEvent>(serialized)!;
                string roundTrip = serializer.SerializeToString(ev)!;
                Assert.Equal(serialized, roundTrip);
                Assert.Equal(expected, ev.Environment);
            }
        }
    }
}
