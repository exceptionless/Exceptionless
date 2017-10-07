using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Serializer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests {
    public class SerializerTests : TestBase {
        public SerializerTests(ITestOutputHelper output) : base(output) {}

        [Fact]
        public void CanDeserializeEventWithUnknownNamesAndProperties() {
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
            Assert.Equal(8, ev.Data.Count);
            Assert.Equal("Hi", ev.Data["SomeString"]);
            Assert.False((bool)ev.Data["SomeBool"]);
            Assert.Equal(1L, ev.Data["SomeNum"]);
            Assert.Equal(typeof(JObject), ev.Data["UnknownProp"].GetType());
            Assert.Equal(typeof(JObject), ev.Data["UnknownSerializedProp"].GetType());
            Assert.Equal("SomeVal", (string)((dynamic)ev.Data["UnknownProp"]).Blah);
            Assert.Equal(typeof(SomeModel), ev.Data["Some"].GetType());
            Assert.Equal(typeof(SomeModel), ev.Data["Some2"].GetType());
            Assert.Equal("SomeVal", ((SomeModel)ev.Data["Some"]).Blah);
            Assert.Equal(typeof(Error), ev.Data[Event.KnownDataKeys.Error].GetType());
            Assert.Equal("SomeVal", ((Error)ev.Data[Event.KnownDataKeys.Error]).Message);
            Assert.Single(((Error)ev.Data[Event.KnownDataKeys.Error]).Data);
            Assert.Equal("SomeVal", ((Error)ev.Data[Event.KnownDataKeys.Error]).Data["SomeProp"]);
            Assert.Equal("Hello", ev.Message);
            Assert.Equal(2, ev.Tags.Count);
            Assert.Contains("One", ev.Tags);
            Assert.Contains("Two", ev.Tags);
            Assert.Equal("12", ev.ReferenceId);

            const string expectedjson = @"{""Tags"":[""One"",""Two""],""Message"":""Hello"",""Data"":{""SomeString"":""Hi"",""SomeBool"":false,""SomeNum"":1,""UnknownProp"":{""Blah"":""SomeVal""},""Some"":{""Blah"":""SomeVal""},""@error"":{""Modules"":[],""Message"":""SomeVal"",""Data"":{""SomeProp"":""SomeVal""},""StackTrace"":[]},""Some2"":{""Blah"":""SomeVal""},""UnknownSerializedProp"":{""Blah"":""SomeVal""}},""ReferenceId"":""12""}";
            string newjson = ev.ToJson(Formatting.None, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore });
            Assert.Equal(expectedjson, newjson);
        }

        [Fact]
        public void CanDeserializeEventWithInvalidKnownDataTypes() {
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
            Assert.Equal(2, ev.Data.Count);
            Assert.True(ev.Data.ContainsKey("Some"));
            Assert.Equal("SomeVal", ((SomeModel)ev.Data["Some"]).Blah);
            Assert.True(ev.Data.ContainsKey("@Some"));
            Assert.Equal("SomeVal", ((SomeModel)ev.Data["@Some"]).Blah);

            ev = jsonWithInvalidDataType.FromJson<Event>(settings);
            Assert.Equal(2, ev.Data.Count);
            Assert.True(ev.Data.ContainsKey("_@Some1"));
            Assert.Equal("Testing", ev.Data["_@Some1"] as string);
            Assert.True(ev.Data.ContainsKey("@string"));
            Assert.Equal("Testing", ev.Data["@string"] as string);
        }

        [Fact]
        public void CanDeserializeEventWithData() {
            const string json = @"{""Message"":""Hello"",""Data"":{""Blah"":""SomeVal""}}";
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new DataObjectConverter<Event>(_logger));

            var ev = json.FromJson<Event>(settings);
            Assert.Single(ev.Data);
            Assert.Equal("Hello", ev.Message);
            Assert.Equal("SomeVal", ev.Data["Blah"]);
        }
    }

    public class SomeModel {
        public string Blah { get; set; }
    }
}
