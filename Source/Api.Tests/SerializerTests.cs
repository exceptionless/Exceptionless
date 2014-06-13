using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using Exceptionless.Serializer;
using Newtonsoft.Json;
using Xunit;

namespace Exceptionless.Api.Tests {
    public class SerializerTests {
        [Fact]
        public void CanDeserializeObjectWithExtensionData() {
            const string json = @"{ ""Message"": ""Hello"", ""UnknownProp"": ""SomeVal"" }";
            var ev = json.FromJson<Event>(new JsonSerializerSettings { ContractResolver = new ExtensionContractResolver() });
            Assert.Equal(1, ev.Data.Count);
            Assert.Equal("SomeVal", ev.Data["UnknownProp"]);
            Assert.Equal("Hello", ev.Message);
        }

        [Fact]
        public void CanRoundTripObjectWithExtensionData() {
            const string json = @"{""Message"":""Hello"",""UnknownProp"":""SomeVal""}";
            var ev = json.FromJson<Event>(new JsonSerializerSettings { ContractResolver = new ExtensionContractResolver(), DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore });
            Assert.Equal(1, ev.Data.Count);
            Assert.Equal("SomeVal", ev.Data["UnknownProp"]);
            Assert.Equal("Hello", ev.Message);

            string newjson = ev.ToJson(Formatting.None, new JsonSerializerSettings { ContractResolver = new ExtensionContractResolver(), DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore });
            Assert.Equal(json, newjson);
        }

        [Fact]
        public void CanRoundTripObjectWithNonPrimitiveExtensionData() {
            const string json = @"{""Message"":""Hello"",""UnknownProp"":{""Blah"":""SomeVal""}}";
            var ev = json.FromJson<Event>(new JsonSerializerSettings { ContractResolver = new ExtensionContractResolver(), DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore });
            Assert.Equal(1, ev.Data.Count);
            Assert.Equal(@"{""Blah"":""SomeVal""}", ev.Data["UnknownProp"]);
            Assert.Equal("Hello", ev.Message);

            const string expectedjson = @"{""Message"":""Hello"",""UnknownProp"":""{\""Blah\"":\""SomeVal\""}""}";
            string newjson = ev.ToJson(Formatting.None, new JsonSerializerSettings { ContractResolver = new ExtensionContractResolver(), DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore });
            Assert.Equal(expectedjson, newjson);
        }
    }
}
