using System;
using System.IO;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Models;
using Nest;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Repositories {
    public sealed class StackIndexTests : ElasticTestBase {
        private readonly IStackRepository _repository;

        public StackIndexTests(ITestOutputHelper output) : base(output) {
            _repository = GetService<IStackRepository>();
            CreateDataAsync().GetAwaiter().GetResult();
        }

        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447a44e78877ab1", 1)]
        [InlineData("2ecd0826e447a44e78877ab2", 1)]
        public async Task GetByIdAsync(string id, int count) {
            var result = await GetByFilterAsync("id:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447ad1e78877555", 3)]
        public async Task GetByOrganizationIdAsync(string id, int count) {
            var result = await GetByFilterAsync("organization:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447ad1e78877ab2", 3)]
        public async Task GetByProjectIdAsync(string id, int count) {
            var result = await GetByFilterAsync("project:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("log", 2)]
        [InlineData("error", 1)]
        [InlineData("custom", 0)]
        public async Task GetByTypeAsync(string type, int count) {
            var result = await GetByFilterAsync("type:" + type);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("[2015-01-08 TO 2015-02-10]", 2)]
        [InlineData("\"2015-01-08T18:29:01.428Z\"", 1)]
        [InlineData("\"2015-02-10T01:05:54.399Z\"", 1)]
        public async Task GetByFirstAsync(string first, int count) {
            var result = await GetByFilterAsync("first:" + first);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"2015-02-03T16:52:41.982Z\"", 1)]
        [InlineData("\"2015-02-11T20:54:04.3457274Z\"", 1)]
        public async Task GetByLastAsync(string last, int count) {
            var result = await GetByFilterAsync("last:" + last);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("(>=20 AND <40)", 1)]
        [InlineData("{5 TO 50}", 1)]
        [InlineData("5", 1)]
        [InlineData("50", 1)]
        public async Task GetByOccurrencesAsync(string occurrences, int count) {
            var result = await GetByFilterAsync("occurrences:" + occurrences);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"GET /Print\"", 1)]
        [InlineData("title:\"GET /Print\"", 1)]
        [InlineData("title:\"The provided anti-forgery token was meant\"", 1)]
        [InlineData("title:\"test@exceptionless.com\"", 1)]
        [InlineData("title:\"Row not found or changed.\"", 1)]
        public async Task GetByTitleAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("tag:test", 1)]
        [InlineData("tag:Blake", 1)]
        [InlineData("tag:Niemyjski", 1)]
        [InlineData("tag:\"Blake Niemyjski\"", 1)]
        public async Task GetByTagAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"2015-02-11T20:54:04.3457274Z\"", 1)]
        public async Task GetByFixedOnAsync(string fixedOn, int count) {
            var result = await GetByFilterAsync("fixedon:" + fixedOn);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(true, 2)]
        public async Task GetByFixedAsync(bool @fixed, int count) {
            var result = await GetByFilterAsync("fixed:" + @fixed.ToString().ToLowerInvariant());
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
        public async Task GetByHiddenAsync(bool hidden, int count) {
            var result = await GetByFilterAsync("hidden:" + hidden.ToString().ToLowerInvariant());
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
        public async Task GetByRegressedAsync(bool regressed, int count) {
            var result = await GetByFilterAsync("regressed:" + regressed.ToString().ToLowerInvariant());
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
        public async Task GetByCriticalAsync(bool critical, int count) {
            var result = await GetByFilterAsync("critical:" + critical.ToString().ToLowerInvariant());
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"http://exceptionless.io\"", 1)]
        [InlineData("links:\"http://exceptionless.io\"", 1)]
        [InlineData("links:\"https://github.com/exceptionless/Exceptionless\"", 1)]
        public async Task GetByLinksAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"my custom description\"", 1)]
        [InlineData("description:\"my custom description\"", 1)]
        public async Task GetByDescriptionAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        private async Task CreateDataAsync() {
            string path = Path.Combine("..", "..", "..", "Search", "Data");
            var serializer = GetService<JsonSerializer>();
            foreach (string file in Directory.GetFiles(path, "stack*.json", SearchOption.AllDirectories)) {
                if (file.EndsWith("summary.json"))
                    continue;

                using (var stream = new FileStream(file, FileMode.Open)) {
                    using (var streamReader = new StreamReader(stream)) {
                        var stack = serializer.Deserialize(streamReader, typeof(Stack)) as Stack;
                        Assert.NotNull(stack);
                        await _repository.AddAsync(stack);
                    }
                }
            }

            await _configuration.Client.RefreshAsync(Indices.All);
        }

        private Task<FindResults<Stack>> GetByFilterAsync(string filter) {
            return _repository.GetByFilterAsync(null, filter, null, null, DateTime.MinValue, DateTime.MaxValue);
        }
    }
}