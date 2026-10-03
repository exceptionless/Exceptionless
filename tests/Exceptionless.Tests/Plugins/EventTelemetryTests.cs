using Exceptionless.Core.Models;
using Exceptionless.Core;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Microsoft.Extensions.Logging;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Validation;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Plugins;

public sealed class EventTelemetryTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Fact]
    public async Task ValidateTelemetryBatch_InvalidObservation_DoesNotRejectNeighboringEvents()
    {
        var organization = new Organization { Id = "123456789012345678901234" };
        var project = new Project { Id = "123456789012345678901235" };
        var valid = new EventContext(new PersistentEvent { Measurements = [new() { Name = "duration", Value = 0, Unit = "ms" }] }, organization, project);
        var invalid = new EventContext(new PersistentEvent { Measurements = [new() { Name = "duration", Unit = "ms" }] }, organization, project);
        var action = new ValidateEventTelemetryAction(GetService<AppOptions>(), GetService<ILoggerFactory>());

        await action.ProcessBatchAsync([valid, invalid]);

        Assert.False(valid.HasError);
        Assert.IsType<MiniValidatorException>(invalid.Exception);
    }

    [Fact]
    public void ValidateTelemetry_DuplicateNamesNullEntriesAndSelfParent_ReturnsErrors()
    {
        var ev = new Event
        {
            ReferenceId = "event-0001",
            ParentReferenceId = "event-0001",
            Result = new string('x', 101),
            Measurements = [new() { Name = "duration", Value = 1, Unit = "s" }, new() { Name = "duration", Value = 2, Unit = "ms" }, null!],
            Labels = new() { ["invalid name"] = "value" }
        };

        var errors = EventTelemetryValidation.GetErrors(ev);

        Assert.Contains(nameof(Event.ParentReferenceId), errors.Keys);
        Assert.Contains(nameof(Event.Measurements), errors.Keys);
        Assert.Contains(nameof(Event.Labels), errors.Keys);
        Assert.Contains(nameof(Event.Result), errors.Keys);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("unknown")]
    public void ParseEvents_NativeTelemetry_RoundTripsWithoutUsingData(string outcome)
    {
        var parser = GetService<EventParserPluginManager>();
        var serializer = GetService<ITextSerializer>();
        string json = $$$"""
            {"type":"operation","reference_id":"child-001","parent_reference_id":"suite-001","root_reference_id":"build-001",
             "outcome":"{{{outcome}}}","result":"completed","measurements":[{"name":"duration","value":0,"unit":"s"},{"name":"allocated","value":128.5,"unit":"By"},{"name":"sql.calls","value":4,"unit":"{call}"}],
             "labels":{"version":"4.90","size":"00123","runtime":"net10"},"value":42,"data":{"@ref:session":"session-001"}}
            """;

        var ev = Assert.Single(parser.ParseEvents(json, 2, null));
        var copy = serializer.Deserialize<PersistentEvent>(serializer.SerializeToString(ev))!;

        Assert.Equal("suite-001", copy.ParentReferenceId);
        Assert.Equal("build-001", copy.RootReferenceId);
        Assert.Equal(outcome, copy.Outcome);
        Assert.Equal("completed", copy.Result);
        Assert.Equal(0, copy.Measurements![0].Value);
        Assert.Equal(128.5, copy.Measurements[1].Value);
        Assert.Equal("{call}", copy.Measurements[2].Unit);
        Assert.Equal("4.90", copy.Labels!["version"]);
        Assert.Equal("00123", copy.Labels["size"]);
        Assert.Equal(42, copy.Value);
        Assert.Equal("session-001", copy.Data!["@ref:session"]);
        Assert.False(copy.Data.ContainsKey("measurements"));
        Assert.False(copy.Data.ContainsKey("labels"));
        Assert.False(copy.Data.ContainsKey("outcome"));
        Assert.False(copy.Data.ContainsKey("result"));
        Assert.Empty(EventTelemetryValidation.GetErrors(copy));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("success", true)]
    [InlineData("failure", true)]
    [InlineData("unknown", true)]
    [InlineData("skipped", false)]
    [InlineData("cancelled", false)]
    [InlineData("SUCCESS", false)]
    [InlineData("", false)]
    public void Validate_NativeOutcome_UsesCanonicalValues(string? outcome, bool valid)
    {
        var errors = EventTelemetryValidation.GetErrors(new Event { Outcome = outcome });

        Assert.Equal(valid, !errors.ContainsKey(nameof(Event.Outcome)));
    }

    [Theory]
    [InlineData("\"measurements\":\"legacy\"", "measurements")]
    [InlineData("\"measurements\":{\"old\":1}", "measurements")]
    [InlineData("\"measurements\":[{\"old\":1}]", "measurements")]
    [InlineData("\"labels\":{\"numeric\":4.90}", "labels")]
    [InlineData("\"labels\":{\"legacy\":null}", "labels")]
    [InlineData("\"outcome\":{\"old\":true}", "outcome")]
    [InlineData("\"outcome\":\"skipped\"", "outcome")]
    [InlineData("\"outcome\":\"SUCCESS\"", "outcome")]
    [InlineData("\"result\":{\"old\":true}", "result")]
    [InlineData("\"dimensions\":{\"branch\":\"main\"}", "dimensions")]
    [InlineData("\"parent_reference_id\":\"legacy\"", "parent_reference_id")]
    public void ParseEvents_LegacyRootValues_PreservesDataAndBatch(string property, string key)
    {
        var parser = GetService<EventParserPluginManager>();

        var events = parser.ParseEvents("[{" + property + ",\"data\":{\"existing\":true}},{\"message\":\"second\"}]", 2, null);

        Assert.Equal(2, events.Count);
        Assert.True(events[0].Data!.ContainsKey(key));
        Assert.True(events[0].Data!.ContainsKey("existing"));
        Assert.Equal("second", events[1].Message);
        Assert.Null(events[0].Outcome);
        Assert.Null(events[0].Result);
        Assert.Null(events[0].Labels);
        Assert.Empty(EventTelemetryValidation.GetErrors(events[0]));
    }

    [Theory]
    [InlineData(Double.NaN)]
    [InlineData(Double.PositiveInfinity)]
    [InlineData(Double.NegativeInfinity)]
    public async Task Validate_NonFiniteMeasurement_RejectsValue(double value)
    {
        var (valid, _) = await GetService<MiniValidationValidator>().ValidateAsync(new EventMeasurement { Name = "duration", Unit = "ms", Value = value });
        Assert.False(valid);
    }

    [Fact]
    public async Task Validate_MissingMeasurementValue_RejectsRatherThanDefaultingToZero()
    {
        var (valid, _) = await GetService<MiniValidationValidator>().ValidateAsync(new EventMeasurement { Name = "duration", Unit = "ms" });
        Assert.False(valid);
    }

    [Fact]
    public void Equals_DifferentTelemetry_DoesNotCollapseObservations()
    {
        var first = new Event { Data = null, Outcome = "success", Measurements = [new() { Name = "duration", Unit = "ms", Value = 0 }] };
        var second = new Event { Data = null, Outcome = "failure", Measurements = [new() { Name = "duration", Unit = "ms", Value = 0 }] };
        Assert.NotEqual(first, second);
        second.Outcome = "success";
        Assert.Equal(first, second);
        second.Result = "completed";
        Assert.NotEqual(first, second);
        second.Result = null;
        second.Measurements[0].Value = 1;
        Assert.NotEqual(first, second);
    }
}
