using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Services;
using Exceptionless.Serializer;
using Foundatio.Repositories.Extensions;
using Foundatio.Serializer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Exceptionless.Tests.Serializer;

public class SerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public SerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void CanDeserializeEventWithUnknownNamesAndProperties()
    {
        const string json = @"{""tags"":[""One"",""Two""],""reference_id"":""12"",""Message"":""Hello"",""SomeString"":""Hi"",""SomeBool"":false,""SomeNum"":1,""UnknownProp"":{""Blah"":""SomeVal""},""Some"":{""Blah"":""SomeVal""},""@error"":{""Message"":""SomeVal"",""SomeProp"":""SomeVal""},""Some2"":""{\""Blah\"":\""SomeVal\""}"",""UnknownSerializedProp"":""{\""Blah\"":\""SomeVal\""}""}";
        var settings = new JsonSerializerSettings();
        var knownDataTypes = new Dictionary<string, Type>
        {
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
        Assert.Equal(false, ev.Data["SomeBool"]);
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
            EventTypes = ["NewError"],
            Version = WebHook.KnownVersions.Version2
        };

        string json = _serializer.SerializeToString(hook);
        Assert.Equal("{\"id\":\"test\",\"event_types\":[\"NewError\"],\"is_enabled\":true,\"version\":\"v2\",\"created_utc\":\"0001-01-01T00:00:00\"}", json);

        var model = _serializer.Deserialize<WebHook>(json);
        Assert.NotNull(model);
        Assert.Equal(hook.Id, model.Id);
        Assert.Equal(hook.EventTypes, model.EventTypes);
        Assert.Equal(hook.Version, model.Version);
    }

    [Fact]
    public void CanDeserializeProject()
    {
        string json = "{\"last_event_date_utc\":\"2020-10-18T20:54:04.3457274+01:00\", \"created_utc\":\"0001-01-01T00:00:00\",\"updated_utc\":\"2020-09-21T04:41:32.7458321Z\"}";

        var model = _serializer.Deserialize<Project>(json);
        Assert.NotNull(model);
        Assert.NotNull(model.LastEventDateUtc);
        Assert.NotEqual(DateTime.MinValue, model.LastEventDateUtc);
        Assert.Equal(DateTime.MinValue, model.CreatedUtc);
        Assert.NotEqual(DateTime.MinValue, model.UpdatedUtc);
    }

    [Fact]
    public void SerializeToString_ValueTupleOfStrings_SerializesFields()
    {
        // Arrange — with IncludeFields=true, ValueTuple fields are serialized.
        // Compile-time names (OrganizationId, etc.) are erased at runtime; fields are always Item1/Item2/Item3.
        // LowerCaseUnderscoreNamingPolicy converts Item1 → item1, Item2 → item2, Item3 → item3.
        var tuple = (OrganizationId: "org1", ProjectId: "proj1", StackId: "stack1");

        // Act
        string json = _serializer.SerializeToString(tuple);

        // Assert
        Assert.Equal("{\"item1\":\"org1\",\"item2\":\"proj1\",\"item3\":\"stack1\"}", json);
    }

    [Fact]
    public void SerializeToString_ValueTupleOfInts_SerializesFields()
    {
        // Arrange
        var tuple = (A: 1, B: 2, C: 3);

        // Act
        string json = _serializer.SerializeToString(tuple);

        // Assert
        Assert.Equal("{\"item1\":1,\"item2\":2,\"item3\":3}", json);
    }

    [Fact]
    public void SerializeToString_ValueTupleOfMixed_SerializesFields()
    {
        // Arrange
        var tuple = (Name: "test", Count: 42, Active: true);

        // Act
        string json = _serializer.SerializeToString(tuple);

        // Assert
        Assert.Equal("{\"item1\":\"test\",\"item2\":42,\"item3\":true}", json);
    }

    [Fact]
    public void SerializeToString_TwoDistinctValueTuples_ProduceDifferentJson()
    {
        // Arrange
        var tuple1 = (OrgId: "org1", ProjId: "proj1", StackId: "stack1");
        var tuple2 = (OrgId: "org2", ProjId: "proj2", StackId: "stack2");

        // Act
        string json1 = _serializer.SerializeToString(tuple1);
        string json2 = _serializer.SerializeToString(tuple2);

        // Assert — distinct tuples produce distinct JSON (no Redis sorted-set collision)
        Assert.Equal("{\"item1\":\"org1\",\"item2\":\"proj1\",\"item3\":\"stack1\"}", json1);
        Assert.Equal("{\"item1\":\"org2\",\"item2\":\"proj2\",\"item3\":\"stack2\"}", json2);
    }

    [Fact]
    public void SerializeToString_ValueTuple_UsesGenericFieldNames()
    {
        // Arrange — ValueTuple field names are erased at runtime; fields are always Item1/Item2/Item3
        // regardless of the compile-time names. Records preserve named properties (organization_id, etc.),
        // making them the correct choice for serialized cache keys.
        var tuple = (OrganizationId: "org1", ProjectId: "proj1", StackId: "stack1");

        // Act
        string json = _serializer.SerializeToString(tuple);

        // Assert — item1/item2/item3, NOT organization_id/project_id/stack_id
        Assert.Equal("{\"item1\":\"org1\",\"item2\":\"proj1\",\"item3\":\"stack1\"}", json);
    }

    [Fact]
    public void SerializeToString_StructWithFields_SerializesFields()
    {
        // Arrange — with IncludeFields=true, structs with public fields serialize correctly
        var value = new FieldOnlyStruct { Key = "abc", Value = 42 };

        // Act
        string json = _serializer.SerializeToString(value);

        // Assert
        Assert.Equal("{\"key\":\"abc\",\"value\":42}", json);
    }

    [Fact]
    public void SerializeToString_StackUsageKey_RoundtripsCorrectly()
    {
        // Arrange
        var key = new StackUsageKey("org1", "proj1", "stack1");

        // Act
        string json = _serializer.SerializeToString(key);
        var deserialized = _serializer.Deserialize<StackUsageKey>(json);

        // Assert
        Assert.Equal("{\"organization_id\":\"org1\",\"project_id\":\"proj1\",\"stack_id\":\"stack1\"}", json);
        Assert.Equal(key, deserialized);
    }

    [Fact]
    public void SerializeToString_DistinctStackUsageKeys_ProduceDifferentJson()
    {
        // Arrange
        var key1 = new StackUsageKey("org1", "proj1", "stack1");
        var key2 = new StackUsageKey("org2", "proj2", "stack2");

        // Act
        string json1 = _serializer.SerializeToString(key1);
        string json2 = _serializer.SerializeToString(key2);

        // Assert
        Assert.NotEqual(json1, json2);
    }

    [Fact]
    public void SerializeToString_RecordStruct_RoundtripsCorrectly()
    {
        // Arrange
        var value = new SampleRecordStruct("key1", 42);

        // Act
        string json = _serializer.SerializeToString(value);
        var deserialized = _serializer.Deserialize<SampleRecordStruct>(json);

        // Assert
        Assert.Equal("{\"key\":\"key1\",\"value\":42}", json);
        Assert.Equal(value, deserialized);
    }

    [Fact]
    public void SerializeToString_ClassWithProperties_RoundtripsCorrectly()
    {
        // Arrange
        var value = new SampleClass { Name = "test", Count = 7 };

        // Act
        string json = _serializer.SerializeToString(value);
        var deserialized = _serializer.Deserialize<SampleClass>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("{\"name\":\"test\",\"count\":7}", json);
        Assert.Equal("test", deserialized.Name);
        Assert.Equal(7, deserialized.Count);
    }

    [Fact]
    public void SerializeToString_PrimitiveTypes_RoundtripCorrectly()
    {
        // Act & Assert — each primitive type verified inline
        Assert.Equal("42", _serializer.SerializeToString(42));
        Assert.Equal("42", _serializer.Deserialize<int>("42").ToString());

        Assert.Equal("99", _serializer.SerializeToString(99L));
        Assert.Equal(99L, _serializer.Deserialize<long>("99"));

        Assert.Equal("true", _serializer.SerializeToString(true));
        Assert.True(_serializer.Deserialize<bool>("true"));

        string? roundtripped = _serializer.Deserialize<string>(_serializer.SerializeToString("hello"));
        Assert.Equal("hello", roundtripped);
    }

    [Fact]
    public void SerializeToString_DateTime_RoundtripsCorrectly()
    {
        // Arrange
        var dt = new DateTime(2026, 2, 22, 12, 0, 0, DateTimeKind.Utc);

        // Act
        string json = _serializer.SerializeToString(dt);
        var deserialized = _serializer.Deserialize<DateTime>(json);

        // Assert
        Assert.Equal(dt, deserialized);
    }

    public struct FieldOnlyStruct
    {
        public string Key;
        public int Value;
    }

    public record struct SampleRecordStruct(string Key, int Value);

    public class SampleClass
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }
}

public record SomeModel
{
    public required string Blah { get; set; }
}
