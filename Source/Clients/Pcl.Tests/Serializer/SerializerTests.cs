using System;
using System.Collections.Generic;
using Exceptionless;
using Exceptionless.Serializer;
using Exceptionless.Extensions;
using Xunit;

namespace Pcl.Tests.Serializer {
    public class SerializerTests {
        protected virtual IJsonSerializer GetSerializer() {
            return new DefaultJsonSerializer();
        }

        [Fact]
        public void CanSerialize() {
            var data = new SampleModel { Date = DateTime.Now, Message = "Testing" };
            IJsonSerializer serializer = GetSerializer();
            string json = serializer.Serialize(data, new[] { "Date" });
            Assert.Equal(@"{""Message"":""Testing""}", json);
        }

        [Fact]
        public void CanExcludeProperties() {
            var data = new SampleModel { Date = DateTime.Now, Message = "Testing" };
            IJsonSerializer serializer = GetSerializer();
            string json = serializer.Serialize(data, new []{ "Date" });
            Assert.Equal(@"{""Message"":""Testing""}", json);
        }

        [Fact]
        public void CanExcludeNestedProperties() {
            var data = new SampleModel { Date = DateTime.Now, Message = "Testing", Nested = new SampleModel { Date = DateTime.Now, Message = "Nested" } };
            IJsonSerializer serializer = GetSerializer();
            string json = serializer.Serialize(data, new[] { "Date" });
            Assert.Equal(@"{""Message"":""Testing"",""Nested"":{""Message"":""Nested""}}", json);
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
            Assert.Equal(@"{""Message"":""Level 1"",""Nested"":{""Message"":""Level 2""}}", json);
        }
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
}
