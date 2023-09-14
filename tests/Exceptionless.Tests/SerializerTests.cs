using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Serializer;
using Foundatio.Repositories.Extensions;
using Foundatio.Serializer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests;

public class SerializerTests : TestWithServices
{
    public SerializerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void CanDeserializeEventWithUnknownNamesAndProperties()
    {
        const string json = @"{""tags"":[""One"",""Two""],""reference_id"":""12"",""Message"":""Hello"",""SomeString"":""Hi"",""SomeBool"":false,""SomeNum"":1,""UnknownProp"":{""Blah"":""SomeVal""},""Some"":{""Blah"":""SomeVal""},""@error"":{""Message"":""SomeVal"",""SomeProp"":""SomeVal""},""Some2"":""{\""Blah\"":\""SomeVal\""}"",""UnknownSerializedProp"":""{\""Blah\"":\""SomeVal\""}""}";
        var settings = new JsonSerializerSettings();
        var knownDataTypes = new Dictionary<string, Type> {
                { "Some", typeof(SomeModel) },
                { "Some2", typeof(SomeModel) },
                { Event.KnownDataKeys.Error, typeof(Error) }
            };
        settings.Converters.Add(new DataObjectConverter<Event>(_logger, knownDataTypes));
        settings.Converters.Add(new DataObjectConverter<Error>(_logger));

        var ev = json.FromJson<Event>(settings);
        Assert.NotNull(ev?.Data);

        Assert.Equal(8, ev.Data.Count);
        Assert.Equal("Hi", ev.Data.GetString("SomeString"));
        Assert.False(ev.Data.GetBoolean("SomeBool"));
        Assert.Equal(1L, ev.Data["SomeNum"]);
        Assert.Equal(typeof(JObject), ev.Data["UnknownProp"]?.GetType());
        Assert.Equal(typeof(JObject), ev.Data["UnknownSerializedProp"]?.GetType());
        Assert.Equal("SomeVal", (string)((dynamic)ev.Data["UnknownProp"]!)?.Blah!);
        Assert.Equal(typeof(SomeModel), ev.Data["Some"]?.GetType());
        Assert.Equal(typeof(SomeModel), ev.Data["Some2"]?.GetType());
        Assert.Equal("SomeVal", (ev.Data["Some"] as SomeModel)?.Blah);
        Assert.Equal(typeof(Error), ev.Data[Event.KnownDataKeys.Error]?.GetType());
        Assert.Equal("SomeVal", ((Error)ev.Data[Event.KnownDataKeys.Error]!)?.Message);
        Assert.Single(((Error)ev.Data[Event.KnownDataKeys.Error]!)?.Data!);
        Assert.Equal("SomeVal", ((Error)ev.Data[Event.KnownDataKeys.Error]!)?.Data?["SomeProp"]);
        Assert.Equal("Hello", ev.Message);
        Assert.NotNull(ev.Tags);
        Assert.Equal(2, ev.Tags.Count);
        Assert.Contains("One", ev.Tags);
        Assert.Contains("Two", ev.Tags);
        Assert.Equal("12", ev.ReferenceId);

        const string expectedjson = @"{""Tags"":[""One"",""Two""],""Message"":""Hello"",""Data"":{""SomeString"":""Hi"",""SomeBool"":false,""SomeNum"":1,""UnknownProp"":{""Blah"":""SomeVal""},""Some"":{""Blah"":""SomeVal""},""@error"":{""Modules"":[],""Message"":""SomeVal"",""Data"":{""SomeProp"":""SomeVal""},""StackTrace"":[]},""Some2"":{""Blah"":""SomeVal""},""UnknownSerializedProp"":{""Blah"":""SomeVal""}},""ReferenceId"":""12""}";
        string newjson = ev.ToJson(Formatting.None, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore });
        Assert.Equal(expectedjson, newjson);
    }

    [Fact]
    public void CanDeserializeEventWithInvalidKnownDataTypes()
    {
        const string json = @"{""Message"":""Hello"",""Some"":""{\""Blah\"":\""SomeVal\""}"",""@Some"":""{\""Blah\"":\""SomeVal\""}""}";
        const string jsonWithInvalidDataType = @"{""Message"":""Hello"",""@Some"":""Testing"",""@string"":""Testing""}";

        var settings = new JsonSerializerSettings();
        var knownDataTypes = new Dictionary<string, Type> {
                { "Some", typeof(SomeModel) },
                { "@Some", typeof(SomeModel) },
                { "_@Some", typeof(SomeModel) },
                { "@string", typeof(string) }
            };
        settings.Converters.Add(new DataObjectConverter<Event>(_logger, knownDataTypes));

        var ev = json.FromJson<Event>(settings);
        Assert.NotNull(ev?.Data);
        Assert.Equal(2, ev.Data.Count);
        Assert.True(ev.Data.ContainsKey("Some"));
        Assert.Equal("SomeVal", (ev.Data["Some"] as SomeModel)?.Blah);
        Assert.True(ev.Data.ContainsKey("@Some"));
        Assert.Equal("SomeVal", (ev.Data["@Some"] as SomeModel)?.Blah);

        ev = jsonWithInvalidDataType.FromJson<Event>(settings);
        Assert.NotNull(ev?.Data);
        Assert.Equal(2, ev.Data.Count);
        Assert.True(ev.Data.ContainsKey("_@Some1"));
        Assert.Equal("Testing", ev.Data["_@Some1"] as string);
        Assert.True(ev.Data.ContainsKey("@string"));
        Assert.Equal("Testing", ev.Data["@string"] as string);
    }

    [Fact]
    public void CanDeserializeEventWithData()
    {
        const string json = @"{""Message"":""Hello"",""Data"":{""Blah"":""SomeVal""}}";
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new DataObjectConverter<Event>(_logger));

        var ev = json.FromJson<Event>(settings);
        Assert.NotNull(ev?.Data);
        Assert.Single(ev.Data);
        Assert.Equal("Hello", ev.Message);
        Assert.Equal("SomeVal", ev.Data["Blah"]);
    }

    [Fact]
    public void CanDeserializeWebHook()
    {
        var hook = new WebHook
        {
            Id = "test",
            EventTypes = new[] { "NewError" },
            Version = WebHook.KnownVersions.Version2
        };

        var serializer = GetService<ITextSerializer>();
        string json = serializer.SerializeToString(hook);
        Assert.Equal("{\"id\":\"test\",\"event_types\":[\"NewError\"],\"is_enabled\":true,\"version\":\"v2\",\"created_utc\":\"0001-01-01T00:00:00\"}", json);

        var model = serializer.Deserialize<WebHook>(json);
        Assert.Equal(hook.Id, model.Id);
        Assert.Equal(hook.EventTypes, model.EventTypes);
        Assert.Equal(hook.Version, model.Version);
    }

    [Fact]
    public void CanDeserializeProject()
    {
        string json = "{\"last_event_date_utc\":\"2020-10-18T20:54:04.3457274+01:00\", \"created_utc\":\"0001-01-01T00:00:00\",\"updated_utc\":\"2020-09-21T04:41:32.7458321Z\"}";

        var serializer = GetService<ITextSerializer>();
        var model = serializer.Deserialize<Project>(json);
        Assert.NotNull(model);
        Assert.NotNull(model.LastEventDateUtc);
        Assert.NotEqual(DateTime.MinValue, model.LastEventDateUtc);
        Assert.Equal(DateTime.MinValue, model.CreatedUtc);
        Assert.NotEqual(DateTime.MinValue, model.UpdatedUtc);
    }
}

public record SomeModel
{
    public required string Blah { get; set; }
}
