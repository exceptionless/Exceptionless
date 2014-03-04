#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ApprovalTests;
using ApprovalTests.Reporters;
using Exceptionless.Client.Tests.Utility;
using Exceptionless.Json;
using Exceptionless.Json.Linq;
using Exceptionless.Models;
using Exceptionless.Serialization;
using Xunit;

namespace Exceptionless.Tests.Serialization {
    [UseReporter(typeof(SmartReporter))]
    public class JsonTests {
        public class Person {
            public Person(string name) {
                Name = name;
                ExtendedData = new DataDictionary();
            }

            public string Name { get; set; }

            public DataDictionary ExtendedData { get; set; }

            public object UnknownData { get; set; }

            [JsonIgnore]
            public string SecureValue { get; set; }

            public Person CircularReference { get { return this; } }
        }

        public class HasPropertyWithException : Person {
            public HasPropertyWithException(string name) : base(name) {}

            public object CantSerializeMe { get { throw new ApplicationException(); } set { } }
        }

        [Fact]
        public void SerializeObjectWithDepthLimit() {
            var data = new {
                PropLevel1 = "value",
                Nested1 = new {
                    PropLevel2 = "value",
                    Nested2 = new {
                        PropLevel3 = "value"
                    }
                }
            };

            string value = ModelSerializer.Current.SerializeToString(data, maxDepth: 1);
            Assert.NotNull(value);
            Assert.True(value.Contains("PropLevel1"));
            Assert.False(value.Contains("Nested1"));
            Assert.False(value.Contains("PropLevel2"));
            Assert.False(value.Contains("Nested2"));
            Assert.False(value.Contains("PropLevel3"));

            value = ModelSerializer.Current.SerializeToString(data, maxDepth: 2);
            Assert.NotNull(value);
            Assert.True(value.Contains("PropLevel1"));
            Assert.True(value.Contains("Nested1"));
            Assert.True(value.Contains("PropLevel2"));
            Assert.False(value.Contains("Nested2"));
            Assert.False(value.Contains("PropLevel3"));

            value = ModelSerializer.Current.SerializeToString(data, maxDepth: 10);
            Assert.NotNull(value);
            Assert.True(value.Contains("PropLevel1"));
            Assert.True(value.Contains("Nested1"));
            Assert.True(value.Contains("PropLevel2"));
            Assert.True(value.Contains("Nested2"));
            Assert.True(value.Contains("PropLevel3"));

            value = ModelSerializer.Current.SerializeToString(data);
            Assert.NotNull(value);
            Assert.True(value.Contains("PropLevel1"));
            Assert.True(value.Contains("Nested1"));
            Assert.True(value.Contains("PropLevel2"));
            Assert.True(value.Contains("Nested2"));
            Assert.True(value.Contains("PropLevel3"));
        }

        [Fact]
        public string SerializePersonToJson() {
            Person person = CreatePerson();
            person.ExtendedData["children"] = CreatePerson("buddy", 2008);

            string value = ModelSerializer.Current.SerializeToString(person);
            Assert.NotNull(value);
            Approvals.Verify(value);

            return value;
        }

        [Fact]
        public void SerializeWithExcludedProperties() {
            var model = new ExcludedPropertiesModel {
                CardFullName = "Blake Niemyjski",
                CardNumber = "4242 4242 4242 4242",
                CardLastFour = "4242",
                Expiration = new DateTime(2013, 1, 1),
                CardType = "Visa",
                UserName = "bniemyjski",
                Password = "nonenone",
                ConfirmPassword = "nonenone",
                PasswordSalt = "5C365DCF-A426-4D8F-9662-2B62DC4FC4E3",
                HashCode = "0F3F5D30-DDE8-4086-8A77-632E85CF542E",
                ResetPasswordAfter90Days = true,
                RememberMe = true,
                SSN = "123-45-6789",
                SocialSecurityNumber = "123-45-6789",
                PhoneNumber = "123-456-7890",
                DateOfBirth = new DateTime(1986, 10, 16),
                EncryptedString = "82171AA4-43F7-47B4-B4E3-2BF6B1CF7F3C"
            };

            string value = ModelSerializer.Current.SerializeToString(model, excludedPropertyNames: new List<string> {
                "*cardnumber*",
                "*password",
                "*ssn*",
                "*socialsecurity*",
                "EncryptedString"
            });
            Assert.NotNull(value);
            Approvals.Verify(value);
        }

        [Fact]
        public string SerializeObjectWithErrors() {
            HasPropertyWithException person = CreateObjectWithException();
            person.ExtendedData["children"] = CreatePerson("buddy", 2008);

            Assert.Throws(typeof(JsonSerializationException), () => ModelSerializer.Current.SerializeToString(person));
            string value = ModelSerializer.Current.SerializeToString(person, ignoreSerializationErrors: true);
            Assert.NotNull(value);
            Approvals.Verify(value);

            return value;
        }

        [Fact]
        public string ExcludePropertyNames() {
            var data = new {
                PropLevel1 = "value",
                Nested1 = new {
                    PropLevel2 = "value",
                    Nested2 = new {
                        PropLevel3 = "value"
                    }
                }
            };

            string value = ModelSerializer.Current.SerializeToString(data, excludedPropertyNames: new[] { "PropLevel3", "ProPlevel2" });
            Assert.NotNull(value);
            Approvals.Verify(value);

            return value;
        }

        [Fact]
        public string ObeysJsonIgnore() {
            Person person = CreatePerson();
            person.ExtendedData["children"] = CreatePerson("buddy", 2008);

            string value = ModelSerializer.Current.SerializeToString(person);
            Assert.NotNull(value);
            Approvals.Verify(value);

            return value;
        }

        [Fact]
        public void DeserializeJsonToProject() {
            Project project;

            const string json = "{\"Name\":\"Test\",\"ApiKeys\":[\"DAE5D68B-31A2-4519-9FB9-09F54D73E8BE\",\"D90CE6D3-ABC3-40FC-B9F2-2D18FA57456F\",\"13D2E6BD-D42E-4120-ADAB-0C1243D565F3\"],\"Configuration\":{\"Settings\":{\"key\":\"test\",\"value\":\"test\"}}}";
            using (var stream = new MemoryStream(Encoding.ASCII.GetBytes(json)))
                project = ModelSerializer.Current.Deserialize<Project>(stream);

            Assert.NotNull(project);
            Assert.NotNull(project.Name);
            Assert.NotNull(project.ApiKeys);
            Assert.NotNull(project.Configuration);
            Assert.NotNull(project.Configuration.Settings);
            Assert.Equal(project.Configuration.Settings.Count, 2);
        }

        [Fact]
        public void DeserializeJsonToProjectWithEmptyConfiguration() {
            Project project;

            const string json = "{\"Name\":\"Test\",\"ApiKeys\":[\"DAE5D68B-31A2-4519-9FB9-09F54D73E8BE\",\"D90CE6D3-ABC3-40FC-B9F2-2D18FA57456F\",\"13D2E6BD-D42E-4120-ADAB-0C1243D565F3\"],\"Configuration\":{\"Settings\":{}}}";
            using (var stream = new MemoryStream(Encoding.ASCII.GetBytes(json)))
                project = ModelSerializer.Current.Deserialize<Project>(stream);

            Assert.NotNull(project);
            Assert.NotNull(project.Name);
            Assert.NotNull(project.ApiKeys);
            Assert.NotNull(project.Configuration);
            Assert.NotNull(project.Configuration.Settings);
            Assert.Equal(project.Configuration.Settings.Count, 0);
        }

        [Fact]
        public void DeserializeJsonToPerson() {
            string json = SerializePersonToJson();

            Person person;
            using (var stream = new MemoryStream(Encoding.ASCII.GetBytes(json)))
                person = ModelSerializer.Current.Deserialize<Person>(stream);

            Assert.NotNull(person);
            Assert.NotNull(person.UnknownData);
            Assert.NotNull(person.ExtendedData);

            var data = person.UnknownData as JObject;
            Assert.NotNull(data);
            Assert.Equal(1, data.Count);
            Assert.NotNull(data["BirthYear"]);
            Assert.Equal(1986, Int32.Parse(data["BirthYear"].ToString()));

            Assert.Equal(2, person.ExtendedData.Count);
            Assert.NotNull(person.ExtendedData["BirthYear"]);
            Assert.True(person.ExtendedData["BirthYear"] is long);
            Assert.Equal(1986, Int32.Parse(person.ExtendedData["BirthYear"].ToString()));
            Assert.NotNull(person.ExtendedData["children"]);
            Assert.True(person.ExtendedData["children"] is string);
            Assert.Equal("{\r\n  \"Name\": \"buddy\",\r\n  \"ExtendedData\": {\r\n    \"BirthYear\": 2008\r\n  },\r\n  \"UnknownData\": {\r\n    \"BirthYear\": 2008\r\n  }\r\n}", person.ExtendedData["children"].ToString());
        }

        private Person CreatePerson(string name = "Blake", int birthYear = 1986) {
            var person = new Person(name) {
                UnknownData = new {
                    BirthYear = birthYear
                }
            };
            person.ExtendedData["BirthYear"] = birthYear;
            person.SecureValue = "1112222333";
            return person;
        }

        private HasPropertyWithException CreateObjectWithException(string name = "Blake", int birthYear = 1986) {
            var person = new HasPropertyWithException(name) {
                UnknownData = new {
                    BirthYear = birthYear
                }
            };
            person.ExtendedData["BirthYear"] = birthYear;
            person.SecureValue = "1112222333";
            return person;
        }

        // TODO: write tests to verify that the dictionaries are case sensitive when serialized and deserialized.
    }
}