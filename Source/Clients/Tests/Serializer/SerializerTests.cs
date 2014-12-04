using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Exceptionless;
using Exceptionless.Core.Serialization;
using Exceptionless.Json;
using Exceptionless.Json.Serialization;
using Exceptionless.Models;
using Exceptionless.Serializer;
using Exceptionless.Extensions;
using Xunit;

namespace Client.Tests.Serializer {
    public class SerializerTests {
        protected virtual IJsonSerializer GetSerializer() {
            return new DefaultJsonSerializer();
        }

        [Fact]
        public void CanSerialize() {
            var data = new SampleModel { Date = DateTime.Now, Message = "Testing" };
            IJsonSerializer serializer = GetSerializer();
            string json = serializer.Serialize(data, new[] { "Date" });
            Assert.Equal(@"{""message"":""Testing""}", json);
        }
         
        [Fact]
        public void CanSerializeEvent() {
            var ev = new Event { Date = DateTime.Now, Message = "Testing"};
            ev.Data["FirstName"] = "Blake";

            IJsonSerializer serializer = GetSerializer();
            string json = serializer.Serialize(ev, new[] { "Date" });
            Assert.Equal(@"{""message"":""Testing"",""data"":{""FirstName"":""Blake""}}", json);
        }


        [Fact]
        public void CanExcludeProperties() {
            var data = new SampleModel { Date = DateTime.Now, Message = "Testing" };
            IJsonSerializer serializer = GetSerializer();
            string json = serializer.Serialize(data, new []{ "Date" });
            Assert.Equal(@"{""message"":""Testing""}", json);
        }

        [Fact]
        public void CanExcludeNestedProperties() {
            var data = new SampleModel { Date = DateTime.Now, Message = "Testing", Nested = new SampleModel { Date = DateTime.Now, Message = "Nested" } };
            IJsonSerializer serializer = GetSerializer();
            string json = serializer.Serialize(data, new[] { "Date" });
            Assert.Equal(@"{""message"":""Testing"",""nested"":{""message"":""Nested""}}", json);
        }

        [Fact]
        public void WillIgnoreDefaultValues() {
            var data = new SampleModel { Number = 0, Bool = false };
            IJsonSerializer serializer = GetSerializer();
            string json = serializer.Serialize(data);
            Assert.Equal(@"{}", json);
            var model = serializer.Deserialize<SampleModel>(json);
            Assert.Equal(data.Number, model.Number);
            Assert.Equal(data.Bool, model.Bool);
        }

        [Fact]
        public void CanSetMaxDepth() {
            var data = new SampleModel { Message = "Level 1", Nested = new SampleModel { Message = "Level 2", Nested = new SampleModel { Message = "Level 3"}} };
            IJsonSerializer serializer = GetSerializer();
            string json = serializer.Serialize(data, maxDepth: 2);
            Assert.Equal(@"{""message"":""Level 1"",""nested"":{""message"":""Level 2""}}", json);
        }

        [Fact]
        public void WillIgnoreEmptyCollections() {
            var data = new SampleModel { Date = DateTime.Now, Message = "Testing", Collection = new Collection<string>() };
            IJsonSerializer serializer = GetSerializer();
            string json = serializer.Serialize(data, new[] { "Date" });
            Assert.Equal(@"{""message"":""Testing""}", json);
        }

        // TODO: Ability to deserialize objects without underscores
        //[Fact]
        public void CanDeserializeDataWithoutUnderscores() {
            const string json = @"{""BlahId"":""Hello""}";
            var settings = new JsonSerializerSettings();
            settings.ContractResolver = new LowerCaseUnderscorePropertyNamesContractResolver();

            var m = JsonConvert.DeserializeObject<Blah>(json, settings);
            Assert.Equal("Hello", m.BlahId);

            string newJson = JsonConvert.SerializeObject(m, settings);
        }
    }

    public class Blah {
        public string BlahId { get; set; }
    }

    public class SampleModel {
        public int Number { get; set; }
        public bool Bool { get; set; }
        public string Message { get; set; }
        public DateTime Date { get; set; }
        public DateTimeOffset DateOffset { get; set; }
        public IDictionary<string, string> Dictionary { get; set; }
        public ICollection<string> Collection { get; set; } 
        public SampleModel Nested { get; set; }
    }

    public class LowerCaseUnderscorePropertyNamesContractResolver : DefaultContractResolver {
        public LowerCaseUnderscorePropertyNamesContractResolver() : base(true) { }

        protected override JsonDictionaryContract CreateDictionaryContract(Type objectType) {
            if (objectType != typeof(DataDictionary) && objectType != typeof(SettingsDictionary))
                return base.CreateDictionaryContract(objectType);

            JsonDictionaryContract contract = base.CreateDictionaryContract(objectType);
            contract.PropertyNameResolver = propertyName => propertyName;
            return contract;
        }

        protected override string ResolvePropertyName(string propertyName) {
            return propertyName.ToLowerUnderscoredWords();
        }
    }
}
