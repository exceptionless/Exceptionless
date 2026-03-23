using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Services;
using Foundatio.Serializer;
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
    public void CanDeserializeEventWithData()
    {
        // Arrange
        /* language=json */
        const string json = """{"message":"Hello","data":{"Blah":"SomeVal"}}""";

        // Act
        var ev = _serializer.Deserialize<Event>(json);

        // Assert
        Assert.NotNull(ev);
        Assert.NotNull(ev.Data);
        Assert.Single(ev.Data);
        Assert.Equal("Hello", ev.Message);
        Assert.Equal("SomeVal", ev.Data["Blah"]);
    }

    [Fact]
    public void CanDeserializeEventWithUnknownNamesAndProperties()
    {
        // Arrange - unknown root properties go through [JsonExtensionData] → ObjectToInferredTypesConverter.
        // The converter recursively converts all JSON values to native .NET types:
        // strings, bools, int/long, nested objects → Dictionary<string, object?>, arrays → List<object?>.
        /* language=json */
        const string json = """{"tags":["One","Two"],"reference_id":"12","message":"Hello","SomeString":"Hi","SomeBool":false,"SomeNum":1,"UnknownProp":{"Blah":"SomeVal"},"UnknownSerializedProp":"{\"Blah\":\"SomeVal\"}"}""";

        // Act
        var ev = _serializer.Deserialize<Event>(json);

        // Assert — verify all properties captured correctly
        Assert.NotNull(ev);
        Assert.NotNull(ev.Data);
        Assert.Equal(5, ev.Data.Count);

        // Primitive types are converted by ObjectToInferredTypesConverter
        Assert.Equal("Hi", ev.Data["SomeString"]);
        Assert.Equal(false, ev.Data["SomeBool"]);
        Assert.Equal(1, ev.Data["SomeNum"]);

        // Unknown nested objects are recursively converted to Dictionary<string, object?>
        Assert.IsType<Dictionary<string, object?>>(ev.Data["UnknownProp"]);
        var unknownProp = (Dictionary<string, object?>)ev.Data["UnknownProp"]!;
        Assert.Equal("SomeVal", unknownProp["Blah"]);

        // Serialized JSON strings stay as strings
        Assert.IsType<string>(ev.Data["UnknownSerializedProp"]);

        Assert.Equal("Hello", ev.Message);
        Assert.NotNull(ev.Tags);
        Assert.Equal(2, ev.Tags.Count);
        Assert.Contains("One", ev.Tags);
        Assert.Contains("Two", ev.Tags);
        Assert.Equal("12", ev.ReferenceId);

        // Verify round-trip preserves data
        string roundTrippedJson = _serializer.SerializeToString(ev);
        var roundTripped = _serializer.Deserialize<Event>(roundTrippedJson);
        Assert.NotNull(roundTripped);
        Assert.Equal(ev.Message, roundTripped.Message);
        Assert.Equal(ev.ReferenceId, roundTripped.ReferenceId);
        Assert.Equal(ev.Tags, roundTripped.Tags);
        Assert.Equal(ev.Data.Count, roundTripped.Data?.Count);
    }

    [Fact]
    public void CanRoundTripEventWithKnownDataTypes()
    {
        // Arrange - Event with known data types (error, request info)
        var originalError = new Error
        {
            Message = "Something went wrong",
            Type = "System.Exception",
            Data = new DataDictionary { { "SomeProp", "SomeVal" } }
        };
        var originalRequest = new RequestInfo { HttpMethod = "GET", Path = "/api/test" };

        var ev = new Event
        {
            Message = "Test error",
            Type = Event.KnownTypes.Error,
            Data = new DataDictionary
            {
                { Event.KnownDataKeys.Error, originalError },
                { Event.KnownDataKeys.RequestInfo, originalRequest }
            }
        };

        // Act
        string json = _serializer.SerializeToString(ev);
        var roundTripped = _serializer.Deserialize<Event>(json);

        // Assert
        Assert.NotNull(roundTripped);
        Assert.Equal(ev.Message, roundTripped.Message);
        Assert.Equal(ev.Type, roundTripped.Type);
        Assert.NotNull(roundTripped.Data);
        Assert.Equal(2, roundTripped.Data.Count);
        Assert.True(roundTripped.Data.ContainsKey(Event.KnownDataKeys.Error));
        Assert.True(roundTripped.Data.ContainsKey(Event.KnownDataKeys.RequestInfo));

        // Verify error data round-tripped with values intact
        var error = roundTripped.Data.GetValue<Error>(Event.KnownDataKeys.Error, _serializer);
        Assert.NotNull(error);
        Assert.Equal(originalError.Message, error.Message);
        Assert.Equal(originalError.Type, error.Type);
        Assert.NotNull(error.Data);
        Assert.Equal("SomeVal", error.Data["SomeProp"]);

        // Verify request info round-tripped
        var request = roundTripped.Data.GetValue<RequestInfo>(Event.KnownDataKeys.RequestInfo, _serializer);
        Assert.NotNull(request);
        Assert.Equal(originalRequest.HttpMethod, request.HttpMethod);
        Assert.Equal(originalRequest.Path, request.Path);
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
        Assert.Equal(hook.Id, model.Id);
        Assert.Equal(hook.EventTypes, model.EventTypes);
        Assert.Equal(hook.Version, model.Version);
    }

    [Fact]
    public void CanDeserializeProject()
    {
        /* language=json */
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

        string roundtripped = _serializer.Deserialize<string>(_serializer.SerializeToString("hello"));
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

    [Fact]
    public void SerializeToString_EnumValues_RoundtripAsCamelCaseStrings()
    {
        // Arrange
        var token = new Token
        {
            Id = "test",
            OrganizationId = "org1",
            ProjectId = "proj1",
            Type = TokenType.Access,
            CreatedBy = "user1",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        // Act
        string json = _serializer.SerializeToString(token);
        var deserialized = _serializer.Deserialize<Token>(json);

        // Assert — enum serializes as camelCase string, not integer
        Assert.Contains("\"type\":\"access\"", json);
        Assert.DoesNotContain("\"type\":1", json);
        Assert.NotNull(deserialized);
        Assert.Equal(TokenType.Access, deserialized.Type);
    }

    [Fact]
    public void SerializeToString_BillingStatusEnum_RoundtripAsCamelCaseStrings()
    {
        // Arrange — BillingStatus.PastDue should serialize as "pastDue" (camelCase)
        var org = new Organization
        {
            Id = "org1",
            Name = "Test",
            BillingStatus = BillingStatus.PastDue
        };

        // Act
        string json = _serializer.SerializeToString(org);
        var deserialized = _serializer.Deserialize<Organization>(json);

        // Assert
        Assert.Contains("\"billing_status\":\"pastDue\"", json);
        Assert.NotNull(deserialized);
        Assert.Equal(BillingStatus.PastDue, deserialized.BillingStatus);
    }

    [Fact]
    public void Deserialize_EnumFromIntegerValue_DeserializesCorrectly()
    {
        // Arrange — backward compatibility: integer enum values should still deserialize
        /* language=json */
        const string json = """{"id":"test","organization_id":"org1","project_id":"proj1","type":1,"created_by":"user1","created_utc":"2026-01-01T00:00:00","updated_utc":"2026-01-01T00:00:00"}""";

        // Act
        var token = _serializer.Deserialize<Token>(json);

        // Assert
        Assert.NotNull(token);
        Assert.Equal(TokenType.Access, token.Type);
    }

    [Fact]
    public void SerializeToString_MixedTypeArrayInDataDictionary_RoundtripsCorrectly()
    {
        // Arrange — DataDictionary with a mixed-type list
        var ev = new Event
        {
            Message = "Test",
            Data = new DataDictionary
            {
                ["mixed"] = new List<object?> { 1, "hello", true, null, 1.5 }
            }
        };

        // Act
        string json = _serializer.SerializeToString(ev);
        var deserialized = _serializer.Deserialize<Event>(json);

        // Assert
        Assert.NotNull(deserialized?.Data);
        Assert.True(deserialized.Data.ContainsKey("mixed"));
        var list = Assert.IsAssignableFrom<IEnumerable<object?>>(deserialized.Data["mixed"]);
        var items = list.ToList();
        Assert.Equal(5, items.Count);
    }

    [Fact]
    public void SerializeToString_NestedDictionaryInDataDictionary_RoundtripsCorrectly()
    {
        // Arrange — 3 levels deep nested dictionary
        var ev = new Event
        {
            Message = "Test",
            Data = new DataDictionary
            {
                ["outer"] = new Dictionary<string, object?>
                {
                    ["inner"] = new Dictionary<string, object?>
                    {
                        ["deep"] = 42
                    }
                }
            }
        };

        // Act
        string json = _serializer.SerializeToString(ev);
        var deserialized = _serializer.Deserialize<Event>(json);

        // Assert
        Assert.NotNull(deserialized?.Data);
        var outer = Assert.IsType<Dictionary<string, object?>>(deserialized.Data["outer"]);
        var inner = Assert.IsType<Dictionary<string, object?>>(outer["inner"]);
        Assert.Equal(42, inner["deep"]);
    }

    [Fact]
    public void SerializeToString_EmptyTagsList_OmittedFromJson()
    {
        // Arrange — event with empty tags should not include "tags" in JSON
        var ev = new Event
        {
            Message = "Test",
            Tags = new TagSet()
        };

        // Act
        string json = _serializer.SerializeToString(ev);

        // Assert — empty collections are suppressed by EmptyCollectionModifier
        Assert.DoesNotContain("\"tags\"", json);
    }
}
