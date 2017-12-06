using System;
using System.IO;
using System.Threading.Tasks;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Api.Tests.Repositories {
    public sealed class EventIndexTests : ElasticTestBase {
        private readonly IEventRepository _repository;
        private readonly PersistentEventQueryValidator _validator;

        public EventIndexTests(ITestOutputHelper output) : base(output) {
            SystemClock.Test.SetFixedTime(new DateTime(2015, 2, 13, 0, 0, 0, DateTimeKind.Utc));
            _repository = GetService<IEventRepository>();
            _validator = GetService<PersistentEventQueryValidator>();
            CreateEventsAsync().GetAwaiter().GetResult();
        }

        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("54dbc16ca0f5c61398427b00", 1)]
        [InlineData("54dbc16ca0f5c61398427b01", 1)]
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
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447a44e78877ab1", 1)]
        [InlineData("2ecd0826e447a44e78877ab2", 2)]
        public async Task GetByStackIdAsync(string id, int count) {
            var result = await GetByFilterAsync("stack:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("000000000", 0)]
        [InlineData("876554321", 1)]
        public async Task GetByReferenceIdAsync(string id, int count) {
            var result = await GetByFilterAsync("reference:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("log", 1)]
        [InlineData("error", 2)]
        [InlineData("custom", 0)]
        public async Task GetByTypeAsync(string type, int count) {
            var result = await GetByFilterAsync("type:" + type);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("_exists_:submission", 1)]
        [InlineData("_missing_:submission", 2)]
        [InlineData("submission:UnobservedTaskException", 1)]
        public async Task GetBySubmissionMethodAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"GET /Print\"", 1)]
        public async Task GetBySourceAsync(string source, int count) {
            var result = await GetByFilterAsync("source:" + source);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("Error", 1)]
        public async Task GetByLevelAsync(string level, int count) {
            var result = await GetByFilterAsync("level:" + level);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"2014-12-09T17:28:44.966\"", 1)]
        [InlineData("\"2014-12-09T17:28:44.966+00:00\"", 1)]
        [InlineData("\"2015-02-11T20:54:04.3457274+00:00\"", 1)]
        public async Task GetByDateAsync(string date, int count) {
            var result = await GetByFilterAsync("date:" + date);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
        public async Task GetByFirstAsync(bool first, int count) {
            var result = await GetByFilterAsync("first:" + first.ToString().ToLowerInvariant());
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"Invalid hash. Parameter name: hash\"", 1)] //see what the actual def is for the standard analyzer
        [InlineData("message:\"Invalid hash. Parameter name: hash\"", 1)]
        public async Task GetByMessageAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("_missing_:tag", 1)]
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
        [InlineData("_missing_:value", 2)]
        [InlineData("_exists_:value", 1)]
        [InlineData("value:1", 1)]
        [InlineData("value:>0", 1)]
        [InlineData("value:(>0 AND <=10)", 1)]
        public async Task GetByValueAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
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
        [InlineData("1", 1)]
        [InlineData("1.2", 1)]
        [InlineData("1.2.3", 1)]
        [InlineData("1.2.3.0", 1)]
        [InlineData("0001.0002.0003.0000", 1)]
        [InlineData("3", 1)]
        [InlineData("3.2", 1)]
        [InlineData("3.2.1", 1)]
        [InlineData("3.2.1.0", 0)]
        [InlineData("3.2.1-beta1", 1)]
        // TODO: Add support for version ranges.
        //[InlineData("<1", 0)]
        //[InlineData(">1", 3)]
        //[InlineData("<5", 3)]
        //[InlineData("(>1 AND <4.0)", 2)]
        public async Task GetByVersionAsync(string version, int count) {
            var result = await GetByFilterAsync("version:" + version);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("SERVER-01", 1)]
        [InlineData("machine:SERVER-01", 1)]
        [InlineData("machine:\"SERVER-01\"", 1)]
        public async Task GetByMachineAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"2001:0:4137:9e76:cfd:33a0:5198:3a66\"", 1)]
        [InlineData("ip:\"2001:0:4137:9e76:cfd:33a0:5198:3a66\"", 1)]
        [InlineData("192.168.0.243", 1)]
        [InlineData("ip:192.168.0.243", 1)]
        [InlineData("192.168.0.88", 1)]
        [InlineData("ip:192.168.0.88", 1)]
        [InlineData("10.0.0.208", 1)]
        [InlineData("ip:10.0.0.208", 1)]
        public async Task GetByIPAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("x86", 0)]
        [InlineData("x64", 1)]
        public async Task GetByArchitectureAsync(string architecture, int count) {
            var result = await GetByFilterAsync("architecture:" + architecture);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("Mozilla", 2)]
        [InlineData("\"Mozilla/5.0\"", 2)]
        [InlineData("5.0", 2)]
        [InlineData("Macintosh", 1)]
        public async Task GetByUserAgentAsync(string userAgent, int count) {
            var result = await GetByFilterAsync("useragent:" + userAgent);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"/user.aspx\"", 1)]
        [InlineData("path:\"/user.aspx\"", 1)]
        public async Task GetByPathAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("browser:Chrome", 2)]
        [InlineData("browser:\"Chrome Mobile\"", 1)]
        public async Task GetByBrowserAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("browser.version:39.0.2171", 1)]
        [InlineData("browser.version:26.0.1410", 1)]
        [InlineData("browser.version:\"26.0.1410\"", 1)]
        public async Task GetByBrowserVersionAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("39", 1)]
        [InlineData("26", 1)]
        public async Task GetByBrowserMajorVersionAsync(string browserMajorVersion, int count) {
            var result = await GetByFilterAsync("browser.major:" + browserMajorVersion);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("device:Huawei", 1)]
        [InlineData("device:\"Huawei U8686\"", 1)]
        public async Task GetByDeviceAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("os:Android", 1)]
        [InlineData("os:Mac", 1)]
        [InlineData("os:\"Mac OS X\"", 1)]
        [InlineData("os:\"Microsoft Windows Server\"", 1)]
        [InlineData("os:\"Microsoft Windows Server 2012 R2 Standard\"", 1)]
        public async Task GetByOSAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("os.version:4.1.1", 1)]
        [InlineData("os.version:10.10.1", 1)]
        [InlineData("os.version:\"10.10\"", 0)]
        [InlineData("os.version:\"10.10.1\"", 1)]
        public async Task GetByOSVersionAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("4", 1)]
        [InlineData("10", 1)]
        public async Task GetByOSMajorVersionAsync(string osMajorVersion, int count) {
            var result = await GetByFilterAsync("os.major:" + osMajorVersion);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("bot:false", 1)]
        [InlineData("-bot:true", 2)]
        [InlineData("bot:true", 1)]
        public async Task GetByBotAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("500", 1)]
        [InlineData("error.code:\"-1\"", 1)]
        [InlineData("error.code:500", 1)]
        [InlineData("error.code:5000", 0)]
        public async Task GetByErrorCodeAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"Invalid hash. Parameter name: hash\"", 1)]
        [InlineData("error.message:\"Invalid hash. Parameter name: hash\"", 1)]
        [InlineData("error.message:\"A Task's exception(s)\"", 1)]
        public async Task GetByErrorMessageAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("AssociateWithCurrentThread", 1)]
        [InlineData("System.Web.ThreadContext.AssociateWithCurrentThread", 1)]
        [InlineData("error.targetmethod:System", 1)]
        [InlineData("error.targetmethod:System.Web", 1)]
        [InlineData("error.targetmethod:System.Web.ThreadContext", 1)]
        [InlineData("error.targetmethod:ThreadContext", 1)]
        [InlineData("error.targetmethod:AssociateWithCurrentThread", 1)]
        [InlineData("error.targetmethod:System.Web.ThreadContext.AssociateWithCurrentThread", 1)]
        [InlineData("error.targetmethod:\"System.Web.ThreadContext.AssociateWithCurrentThread()\"", 1)]
        public async Task GetByErrorTargetMethodAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("Exception", 2)]
        [InlineData("error.targettype:Exception", 1)]
        [InlineData("error.targettype:\"System.Exception\"", 1)]
        public async Task GetByErrorTargetTypeAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("NullReferenceException", 1)]
        [InlineData("System.NullReferenceException", 1)]
        [InlineData("error.type:NullReferenceException", 1)]
        [InlineData("error.type:System.NullReferenceException", 1)]
        [InlineData("error.type:System.Exception", 1)]
        public async Task GetByErrorTypeAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("My-User-Identity", 1)]
        [InlineData("user:My-User-Identity", 1)]
        [InlineData("example@exceptionless.com", 1)]
        [InlineData("user:example@exceptionless.com", 1)]
        [InlineData("user:exceptionless.com", 1)]
        [InlineData("example", 1)]
        public async Task GetByUserAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("Blake", 2)] // Matches due to user name and partial tag
        [InlineData("user.name:Blake", 1)]
        public async Task GetByUserNameAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("test@exceptionless.com", 1)]
        [InlineData("user.email:test@exceptionless.com", 1)]
        [InlineData("user.email:exceptionless.com", 1)]
        public async Task GetByUserEmailAddressAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"my custom description\"", 1)]
        [InlineData("user.description:\"my custom description\"", 1)]
        public async Task GetByUserDescriptionAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("data.anumber:12", 1)]
        [InlineData("data.anumber:>11", 1)]
        [InlineData("data.anumber2:>11", 0)]
        //[InlineData("data.FriendlyErrorIdentifier:\"Foo-7967BB\"", 1)]
        [InlineData("data.some-date:>2015-01-01", 1)]
        [InlineData("data.some-date:<2015-01-01", 0)]
        public async Task GetByCustomDataAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        private async Task CreateEventsAsync() {
            string path = Path.Combine("..", "..", "..", "Search", "Data");
            var parserPluginManager = GetService<EventParserPluginManager>();
            foreach (string file in Directory.GetFiles(path, "event*.json", SearchOption.AllDirectories)) {
                if (file.EndsWith("summary.json"))
                    continue;

                var events = parserPluginManager.ParseEvents(File.ReadAllText(file), 2, "exceptionless/2.0.0.0");
                Assert.NotNull(events);
                Assert.True(events.Count > 0);
                foreach (var ev in events)
                    ev.CopyDataToIndex(Array.Empty<string>());

                await _repository.AddAsync(events, o => o.ImmediateConsistency());
            }

            GetService<ExceptionlessElasticConfiguration>().Events.Event.QueryParser.Configuration.RefreshMapping();
        }

        private async Task<FindResults<PersistentEvent>> GetByFilterAsync(string filter) {
            var result = await _validator.ValidateQueryAsync(filter);
            Assert.True(result.IsValid);
            Log.SetLogLevel<EventRepository>(LogLevel.Trace);
            return await _repository.GetByFilterAsync(null, filter, null, null, DateTime.MinValue, DateTime.MaxValue);
        }
    }
}
