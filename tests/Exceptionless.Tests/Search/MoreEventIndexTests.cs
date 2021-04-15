using System;
using System.Threading.Tasks;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;

namespace Exceptionless.Tests.Repositories {
    public sealed class MoreEventIndexTests : IntegrationTestsBase {
        private readonly IEventRepository _repository;
        private readonly PersistentEventQueryValidator _validator;

        public MoreEventIndexTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
            TestSystemClock.SetFrozenTime(new DateTime(2015, 2, 13, 0, 0, 0, DateTimeKind.Utc));
            _repository = GetService<IEventRepository>();
            _validator = GetService<PersistentEventQueryValidator>();
        }

        [Theory]
        [InlineData("source:\"GET /Print\"", 1)]
        [InlineData("source:\"Gotham Bagle Company\"", 1)]
        [InlineData("source:\"Exceptionless.Web.GeT.Print.SomeClass\"", 1)]
        [InlineData("source:Exceptionless.Web.GET.Print.SomeClass", 1)]
        [InlineData("source:exceptionless.web.gET.print.someClass", 1)]
        [InlineData("source:some/web/path", 1)]
        [InlineData("source:some\\\\/web*", 1)]
        [InlineData("source:Exceptionless*", 2)]
        [InlineData("source:exceptionless.web.gET.p*", 1)]
        [InlineData("source:Exceptionless", 2)]
        [InlineData("source:\"Exceptionless\"", 2)]
        [InlineData("source:GET", 2)]
        [InlineData("source:gEt", 2)]
        [InlineData("source:Print", 2)]
        [InlineData("source:\"/Print\"", 1)]
        [InlineData("source:Bagle", 1)]
        [InlineData("source:exceptionless.web*", 1)]
        [InlineData("source:reason", 1)]
        [InlineData("source:randomText", 1)]
        [InlineData("source:getUrlV2", 1)]
        [InlineData("source:namespace.controller.getUrlV2", 1)]
        [InlineData("source:namespace.controller", 1)]
        [InlineData("source:blake", 1)]
        [InlineData("source:System.Text.StringBuilder", 1)]
        [InlineData("source:System.Text", 1)]
        [InlineData("source:System.Text.StringBuilder,System.Text", 1)]
        public async Task GetBySourceAsync(string search, int count) {
            Log.MinimumLevel = LogLevel.Trace;

            await CreateDataAsync(d => {
                d.Event().Source("Exceptionless.Web.GET.Print.SomeClass");
                d.Event().Source("some/web/path");
                d.Event().Source("Exceptionless");
                d.Event().Source("GET /Print");
                d.Event().Source("Gotham Bagle Company");
                d.Event().Source("System.Text.StringBuilder,System.Text");
                d.Event().Source("randomText,namespace.controller.getUrlV2 (blake) reason https://10.0.1.1:1234/namespace/v2/controller/getUrl?mode=summary&message=test reason2");
            });

            Log.SetLogLevel<EventRepository>(LogLevel.Trace);

            var result = await GetEventsAsync(search);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("exceptionless", 3)]
        [InlineData("exceptionless.com", 3)]
        [InlineData("test@exceptionless.com", 2)]
        [InlineData("user.email:test@exceptionless.com", 2)]
        [InlineData("user.email:exceptionless.com", 3)]
        public async Task GetByUserEmailAddressAsync(string search, int count) {
            await CreateDataAsync(d => {
                d.Event().UserDescription("test@exceptionless.com", "");
                d.Event().UserIdentity("test@exceptionless.com");
                d.Event().UserIdentity("eric@exceptionless.com");
                d.Event().UserIdentity("eric@ericjsmith.net");
                d.Event().UserIdentity("blake.niemyjski@codesmithtools.com");
            });

            var result = await GetEventsAsync(search);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("type:log", 1)]
        [InlineData("type:error", 1)]
        [InlineData("type:custom", 1)]
        public async Task GetByTypeAsync(string search, int count) {
            await CreateDataAsync(d => {
                d.Event().Type(Event.KnownTypes.Log);
                d.Event().Type(Event.KnownTypes.Error);
                d.Event().Type("custom");
            });

            var result = await GetEventsAsync(search);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("NOT _exists_:tag", 1)]
        [InlineData("tag:test", 1)]
        [InlineData("tag:Blake", 0)]
        [InlineData("tag:Niemyjski", 0)]
        [InlineData("tag:\"Blake Niemyjski\"", 1)]
        public async Task GetByTagAsync(string search, int count) {
            await CreateDataAsync(d => {
                d.Event().Tag("Blake Niemyjski");
                d.Event().Tag("test");
                d.Event();
            });

            var result = await GetEventsAsync(search);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("project:000000000000000000000000", 0)]
        [InlineData("project:" + SampleDataService.TEST_PROJECT_ID, 1)]
        [InlineData("project:" + SampleDataService.FREE_PROJECT_ID, 1)]
        [InlineData("project:123", 1)]
        public async Task GetByProjectIdAsync(string search, int count) {
            await CreateDataAsync(d => {
                d.Event().TestProject();
                d.Event().FreeProject();
                d.Event().Project("123");
            });

            var result = await GetEventsAsync(search);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("organization:000000000000000000000000", 0)]
        [InlineData("organization:" + SampleDataService.TEST_ORG_ID, 1)]
        [InlineData("organization:" + SampleDataService.FREE_ORG_ID, 1)]
        [InlineData("organization:123", 1)]
        public async Task GetByOrganizationIdAsync(string search, int count) {
            await CreateDataAsync(d => {
                d.Event().TestProject();
                d.Event().FreeProject();
                d.Event().Organization("123");
            });

            var result = await GetEventsAsync(search);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("stack:000000000000000000000000", 0)]
        [InlineData("stack:1ecd0826e447a44e78877ab1", 2)]
        [InlineData("stack:2ecd0826e447a44e78877ab2", 1)]
        public async Task GetByStackIdAsync(string search, int count) {
            await CreateDataAsync(d => {
                var stack1 = d.Event().StackId("1ecd0826e447a44e78877ab1");
                d.Event().Stack(stack1);

                d.Event().StackId("2ecd0826e447a44e78877ab2");
            });

            var result = await GetEventsAsync(search);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        private async Task<FindResults<PersistentEvent>> GetEventsAsync(string search) {
            var result = await _validator.ValidateQueryAsync(search);
            Assert.True(result.IsValid);
            Log.SetLogLevel<EventRepository>(LogLevel.Trace);
            return await _repository.FindAsync(q => q.SearchExpression(search));
        }
    }
}
