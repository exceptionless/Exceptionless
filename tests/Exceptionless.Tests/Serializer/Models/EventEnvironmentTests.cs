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
        // Arrange
        var serializer = GetService<ITextSerializer>();
        string input = """{"PROPERTY":" Production ","data":{"@environment":{"machine_name":"worker-1"},"environment":{"custom":true}}}""".Replace("PROPERTY", property);

        // Act
        var ev = serializer.Deserialize<Event>(input);
        string json = serializer.SerializeToString(ev)!;
        var roundTrip = serializer.Deserialize<Event>(json);

        // Assert
        Assert.NotNull(ev);
        Assert.Equal("Production", ev.Environment);
        Assert.Equal("worker-1", ev.GetEnvironmentInfo(serializer, _logger)?.MachineName);
        Assert.NotNull(ev.Data?["environment"]);
        Assert.Contains("\"environment\":\"Production\"", json);
        Assert.Equal("Production", roundTrip?.Environment);
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
        // Arrange
        var parser = GetService<JsonEventParserPlugin>();
        string input = $$"""[{"type":"error","message":"first","environment":{{environment}}},{"type":"log","message":"second","environment":"staging"}]""";

        // Act
        var events = parser.ParseEvents(input, 2, null);

        // Assert
        Assert.NotNull(events);
        Assert.Equal(2, events.Count);
        Assert.Null(events[0].Environment);
        Assert.Equal("first", events[0].Message);
        Assert.Equal("staging", events[1].Environment);
    }

    [Fact]
    public void Serialize_UnspecifiedEnvironment_OmitsProperty()
    {
        // Arrange
        var serializer = GetService<ITextSerializer>();

        // Act
        string serialized = serializer.SerializeToString(new Event())!;
        var deserialized = serializer.Deserialize<Event>("{}");
        var tooLong = new Event { Environment = new string('x', 65) };
        var maxLength = new Event { Environment = new string('X', 64) };

        // Assert
        Assert.DoesNotContain("environment", serialized);
        Assert.Null(deserialized?.Environment);
        Assert.Null(tooLong.Environment);
        Assert.Equal(new string('X', 64), maxLength.Environment);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("{\"region\":\"west\"}")]
    [InlineData("[\"production\",\"staging\"]")]
    [InlineData("\"   \"")]
    [InlineData("\"bad\\nenvironment\"")]
    [InlineData("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklm\"")]
    public void Deserialize_InvalidRootEnvironment_PreservesCustomDataRegardlessOfPropertyOrder(string environment)
    {
        // Arrange
        var serializer = GetService<ITextSerializer>();
        using var expected = JsonDocument.Parse(environment);
        var variants = new[] { (true, false), (false, false), (true, true), (false, true) };

        // Act
        var results = new List<(bool DuplicateKey, Event? Event, string Serialized, string? Reserialized)>();
        foreach (var (environmentFirst, duplicateKey) in variants)
        {
            string environmentProperty = $"\"environment\":{environment}";
            string dataProperty = duplicateKey
                ? "\"data\":{\"environment\":\"nested\",\"kept\":true}"
                : "\"data\":{\"kept\":true}";
            string json = environmentFirst
                ? $"{{{environmentProperty},{dataProperty}}}"
                : $"{{{dataProperty},{environmentProperty}}}";
            var ev = serializer.Deserialize<Event>(json);
            string serialized = serializer.SerializeToString(ev)!;
            string? reserialized = serializer.SerializeToString(serializer.Deserialize<Event>(serialized));
            results.Add((duplicateKey, ev, serialized, reserialized));
        }

        // Assert
        foreach (var (duplicateKey, ev, serialized, reserialized) in results)
        {
            Assert.NotNull(ev);
            Assert.Null(ev.Environment);
            using var result = JsonDocument.Parse(serialized);
            var data = result.RootElement.GetProperty("data");
            Assert.True(data.GetProperty("kept").GetBoolean());
            Assert.True(JsonElement.DeepEquals(expected.RootElement, data.GetProperty(duplicateKey ? "environment1" : "environment")));
            if (duplicateKey)
            {
                Assert.Equal("nested", data.GetProperty("environment").GetString());
            }
            Assert.False(result.RootElement.TryGetProperty("environment", out _));
            Assert.Equal(serialized, reserialized);
        }
    }

    [Fact]
    public void Equals_DifferentEnvironments_DistinguishesEvents()
    {
        // Arrange
        var production = new Event { Environment = "production", Data = null };
        var staging = new Event { Environment = "staging", Data = null };
        var paddedProduction = new Event { Environment = " Production ", Data = null };
        var capitalizedProduction = new Event { Environment = "Production", Data = null };

        // Act
        bool productionEqualsStaging = production.Equals(staging);
        bool paddedEqualsCapitalized = paddedProduction.Equals(capitalizedProduction);
        bool capitalizedEqualsProduction = capitalizedProduction.Equals(production);

        // Assert
        Assert.False(productionEqualsStaging);
        Assert.True(paddedEqualsCapitalized);
        Assert.False(capitalizedEqualsProduction);
    }

    [Theory]
    [InlineData("environment", "west", "west")]
    [InlineData("Environment", "west", "west")]
    [InlineData("ENVIRONMENT", " Production ", "Production")]
    public void ParseEvents_ValidEnvironment_StoresDeploymentNameOnlyAndRoundTrips(string propertyName, string environment, string expected)
    {
        // Arrange
        var parser = GetService<JsonEventParserPlugin>();
        var serializer = GetService<ITextSerializer>();
        string input = $$$"""{"{{{propertyName}}}":{{{JsonSerializer.Serialize(environment)}}},"data":{"kept":true}}""";

        // Act
        var parsed = parser.ParseEvents(input, 2, null);
        string? serialized = serializer.SerializeToString(parsed?.SingleOrDefault());
        var reparsed = parser.ParseEvents(serialized!, 2, null);

        // Assert
        var ev = Assert.Single(Assert.IsType<List<PersistentEvent>>(parsed));
        Assert.Equal(expected, ev.Environment);
        Assert.NotNull(ev.Data);
        Assert.False(ev.Data.ContainsKey("environment"));
        var roundTrip = Assert.Single(Assert.IsType<List<PersistentEvent>>(reparsed));
        Assert.Equal(serialized, serializer.SerializeToString(roundTrip));
    }
}
