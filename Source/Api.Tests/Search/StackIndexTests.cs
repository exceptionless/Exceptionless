using System;
using System.Collections.Generic;
using System.IO;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Serialization;
using Exceptionless.Core.Models;
using Nest;
using Newtonsoft.Json;
using Xunit;
using Xunit.Extensions;
using SortOrder = Exceptionless.Core.Repositories.SortOrder;

namespace Exceptionless.Api.Tests.Repositories {
    public class StackIndexTests {
        private readonly IStackRepository _repository = IoC.GetInstance<IStackRepository>();
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private static bool _createdStacks;

        public StackIndexTests() {
            if (!_createdStacks) {
                _createdStacks = true;
                CreateStacks();
            }
        }

        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447a44e78877ab1", 1)]
        [InlineData("2ecd0826e447a44e78877ab2", 1)]
        public void GetById(string id, int count) {
            var result = GetByFilter("id:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447ad1e78877555", 3)]
        public void GetByOrganizationId(string id, int count) {
            var result = GetByFilter("organization:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447ad1e78877ab2", 3)]
        public void GetByProjectId(string id, int count) {
            var result = GetByFilter("project:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("log", 2)]
        [InlineData("error", 1)]
        [InlineData("custom", 0)]
        public void GetByType(string type, int count) {
            var result = GetByFilter("type:" + type);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("[2015-01-08 TO 2015-02-10]", 2)]
        [InlineData("\"2015-01-08T18:29:01.428Z\"", 1)]
        [InlineData("\"2015-02-10T01:05:54.399Z\"", 1)]
        public void GetByFirst(string first, int count) {
            var result = GetByFilter("first:" + first);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("\"2015-02-03T16:52:41.982Z\"", 1)]
        [InlineData("\"2015-02-11T20:54:04.3457274Z\"", 1)]
        public void GetByLast(string last, int count) {
            var result = GetByFilter("last:" + last);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("(>=20 AND <40)", 1)]
        [InlineData("{5 TO 50}", 1)]
        [InlineData("5", 1)]
        [InlineData("50", 1)]
        public void GetByOccurrences(string occurrences, int count) {
            var result = GetByFilter("occurrences:" + occurrences);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("\"GET /Print\"", 1)]
        [InlineData("title:\"GET /Print\"", 1)]
        [InlineData("title:\"The provided anti-forgery token was meant\"", 1)]
        [InlineData("title:\"test@exceptionless.com\"", 1)]
        [InlineData("title:\"Row not found or changed.\"", 1)]
        public void GetByTitle(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("tag:test", 1)]
        [InlineData("tag:Blake", 1)]
        [InlineData("tag:Niemyjski", 1)]
        [InlineData("tag:\"Blake Niemyjski\"", 1)]
        public void GetByTag(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("\"2015-02-11T20:54:04.3457274Z\"", 1)]
        public void GetByFixedOn(string fixedOn, int count) {
            var result = GetByFilter("fixedon:" + fixedOn);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(true, 2)]
        public void GetByFixed(bool @fixed, int count) {
            var result = GetByFilter("fixed:" + @fixed.ToString().ToLower());
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
        public void GetByHidden(bool hidden, int count) {
            var result = GetByFilter("hidden:" + hidden.ToString().ToLower());
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
        public void GetByRegressed(bool regressed, int count) {
            var result = GetByFilter("regressed:" + regressed.ToString().ToLower());
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
        public void GetByCritical(bool critical, int count) {
            var result = GetByFilter("critical:" + critical.ToString().ToLower());
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("\"http://exceptionless.io\"", 1)]
        [InlineData("links:\"http://exceptionless.io\"", 1)]
        [InlineData("links:\"https://github.com/exceptionless/Exceptionless\"", 1)]
        public void GetByLinks(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("\"my custom description\"", 1)]
        [InlineData("description:\"my custom description\"", 1)]
        public void GetByDescription(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        private void CreateStacks() {
            ElasticSearchConfiguration.ConfigureMapping(_client, true);

            var serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings {
                ContractResolver = new LowerCaseUnderscorePropertyNamesContractResolver()
            });

            foreach (var file in Directory.GetFiles(@"..\..\Search\Data\", "stack*.json", SearchOption.AllDirectories)) {
                using (var stream = new FileStream(file, FileMode.Open)) {
                    using (var streamReader = new StreamReader(stream)) {
                        var stack = serializer.Deserialize(streamReader, typeof(Stack)) as Stack;
                        Assert.NotNull(stack);
                        _repository.Add(stack);
                    }
                }
            }

            _client.Refresh();
        }

        private ICollection<Stack> GetByFilter(string filter) {
            return _repository.GetByFilter(null, filter, null, SortOrder.Descending, null, DateTime.MinValue, DateTime.MaxValue, new PagingOptions());
        }
    }
}