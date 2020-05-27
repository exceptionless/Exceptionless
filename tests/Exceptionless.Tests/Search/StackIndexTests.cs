using System;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Repositories {
    public sealed class StackIndexTests : IntegrationTestsBase {
        private readonly IStackRepository _repository;

        public StackIndexTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
            _repository = GetService<IStackRepository>();
        }
        
        protected override async Task ResetDataAsync() {
            await base.ResetDataAsync();
            await StackData.CreateSearchDataAsync(_repository, GetService<JsonSerializer>());
        }

        [Theory]
        [InlineData("\"GET /Print\"", 2)] // Title
        [InlineData("\"my custom description\"", 1)] // Description
        [InlineData("\"Blake Niemyjski\"", 1)] // Tags
        [InlineData("\"http://exceptionless.io\"", 2)] // References
        public async Task GetByAllFieldAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }
        
        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447a44e78877ab1", 1)]
        [InlineData("2ecd0826e447a44e78877ab2", 1)]
        public async Task GetAsync(string id, int count) {
            var result = await GetByFilterAsync("id:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447ad1e78877555", 4)]
        public async Task GetByOrganizationIdAsync(string id, int count) {
            var result = await GetByFilterAsync("organization:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447ad1e78877ab2", 4)]
        public async Task GetByProjectIdAsync(string id, int count) {
            var result = await GetByFilterAsync("project:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("log", 3)]
        [InlineData("error", 1)]
        [InlineData("custom", 0)]
        public async Task GetByTypeAsync(string type, int count) {
            var result = await GetByFilterAsync("type:" + type);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("[2015-01-08 TO 2015-02-10]", 3)]
        [InlineData("\"2015-01-08T18:29:01.428Z\"", 1)]
        [InlineData("\"2015-02-10T01:05:54.399Z\"", 2)]
        public async Task GetByFirstAsync(string first, int count) {
            var result = await GetByFilterAsync("first:" + first);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"2015-02-03T16:52:41.982Z\"", 1)]
        [InlineData("\"2015-02-11T20:54:04.3457274Z\"", 2)]
        public async Task GetByLastAsync(string last, int count) {
            var result = await GetByFilterAsync("last:" + last);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("{5 TO 50}", 1)]
        [InlineData("5", 2)]
        [InlineData("50", 1)]
        public async Task GetByOccurrencesAsync(string occurrences, int count) {
            var result = await GetByFilterAsync("occurrences:" + occurrences);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("title:\"GET /Print\"", 2)]
        [InlineData("title:\"The provided anti-forgery token was meant\"", 1)]
        [InlineData("title:\"test@exceptionless.com\"", 1)]
        [InlineData("title:\"Row not found or changed.\"", 1)]
        public async Task GetByTitleAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("tag:test", 2)]
        [InlineData("tag:Blake", 0)]
        [InlineData("tag:Niemyjski", 0)]
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
        [InlineData("open", 1)]
        [InlineData("fixed", 2)]
        [InlineData("regressed", 1)]
        public async Task GetByStatusAsync(string status, int count) {
            var result = await GetByFilterAsync("status:" + status);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData(false, 3)]
        [InlineData(true, 1)]
        public async Task GetByCriticalAsync(bool critical, int count) {
            var result = await GetByFilterAsync("critical:" + critical.ToString().ToLowerInvariant());
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("links:\"http://exceptionless.io\"", 2)]
        [InlineData("links:\"https://github.com/exceptionless/Exceptionless\"", 1)]
        public async Task GetByLinksAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("description:\"my custom description\"", 1)]
        public async Task GetByDescriptionAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        private Task<QueryResults<Stack>> GetByFilterAsync(string filter) {
            return _repository.QueryAsync(q => q.FilterExpression(filter));
        }
    }
}