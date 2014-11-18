using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Exceptionless.Serializer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Exceptionless.Api.Tests {
    public class SerializerTests {
        [Fact]
        public void CanDeserializeEventWithUnknownNamesAndProperties() {
            const string json = @"{""tags"":[""One"",""Two""],""reference_id"":""12"",""Message"":""Hello"",""SomeString"":""Hi"",""SomeBool"":false,""SomeNum"":1,""UnknownProp"":{""Blah"":""SomeVal""},""Some"":{""Blah"":""SomeVal""},""error"":{""Message"":""SomeVal"",""SomeProp"":""SomeVal""},""Some2"":""{\""Blah\"":\""SomeVal\""}"",""UnknownSerializedProp"":""{\""Blah\"":\""SomeVal\""}""}";
            var settings = new JsonSerializerSettings();
            var knownDataTypes = new Dictionary<string, Type> {
                { "Some", typeof(SomeModel) },
                { "Some2", typeof(SomeModel) },
                { Event.KnownDataKeys.Error, typeof(Error) }
            };
            settings.Converters.Add(new DataObjectConverter<Event>(knownDataTypes));
            settings.Converters.Add(new DataObjectConverter<Error>());

            var ev = json.FromJson<Event>(settings);
            Assert.Equal(8, ev.Data.Count);
            Assert.Equal("Hi", ev.Data["SomeString"]);
            Assert.Equal(false, ev.Data["SomeBool"]);
            Assert.Equal(1L, ev.Data["SomeNum"]);
            Assert.Equal(typeof(JObject), ev.Data["UnknownProp"].GetType());
            Assert.Equal(typeof(JObject), ev.Data["UnknownSerializedProp"].GetType());
            Assert.Equal("SomeVal", (string)((dynamic)ev.Data["UnknownProp"]).Blah);
            Assert.Equal(typeof(SomeModel), ev.Data["Some"].GetType());
            Assert.Equal(typeof(SomeModel), ev.Data["Some2"].GetType());
            Assert.Equal("SomeVal", ((SomeModel)ev.Data["Some"]).Blah);
            Assert.Equal(typeof(Error), ev.Data[Event.KnownDataKeys.Error].GetType());
            Assert.Equal("SomeVal", ((Error)ev.Data[Event.KnownDataKeys.Error]).Message);
            Assert.Equal(1, ((Error)ev.Data[Event.KnownDataKeys.Error]).Data.Count);
            Assert.Equal("SomeVal", ((Error)ev.Data[Event.KnownDataKeys.Error]).Data["SomeProp"]);
            Assert.Equal("Hello", ev.Message);
            Assert.Equal(2, ev.Tags.Count);
            Assert.True(ev.Tags.Contains("One"));
            Assert.True(ev.Tags.Contains("Two"));
            Assert.Equal("12", ev.ReferenceId);

            const string expectedjson = @"{""Tags"":[""One"",""Two""],""Message"":""Hello"",""Data"":{""SomeString"":""Hi"",""SomeBool"":false,""SomeNum"":1,""UnknownProp"":{""Blah"":""SomeVal""},""Some"":{""Blah"":""SomeVal""},""error"":{""Modules"":[],""Message"":""SomeVal"",""Data"":{""SomeProp"":""SomeVal""},""StackTrace"":[]},""Some2"":{""Blah"":""SomeVal""},""UnknownSerializedProp"":{""Blah"":""SomeVal""}},""ReferenceId"":""12""}";
            string newjson = ev.ToJson(Formatting.None, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore });
            Assert.Equal(expectedjson, newjson);
        }

        [Fact]
        public void CanDeserializeEventWithData() {
            const string json = @"{""Message"":""Hello"",""Data"":{""Blah"":""SomeVal""}}";
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new DataObjectConverter<Event>());

            var ev = json.FromJson<Event>(settings);
            Assert.Equal(1, ev.Data.Count);
            Assert.Equal("Hello", ev.Message);
            Assert.Equal("SomeVal", ev.Data["Blah"]);
        }
    }

    public class SomeModel {
        public string Blah { get; set; }
    }
}
