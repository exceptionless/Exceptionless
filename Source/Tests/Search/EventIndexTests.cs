using System;
using System.IO;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Nest;
using Xunit;
using SortOrder = Exceptionless.Core.Repositories.SortOrder;

namespace Exceptionless.Api.Tests.Repositories {
    public class EventIndexTests {
        private readonly IEventRepository _repository = IoC.GetInstance<IEventRepository>();
        private readonly ElasticSearchConfiguration _configuration = IoC.GetInstance<ElasticSearchConfiguration>();
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        
        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("54dbc16ca0f5c61398427b00", 1)]
        [InlineData("54dbc16ca0f5c61398427b01", 1)]
        public async Task GetById(string id, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("id:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447ad1e78877555", 3)]
        public async Task GetByOrganizationId(string id, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("organization:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }
        
        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447ad1e78877ab2", 3)]
        public async Task GetByProjectId(string id, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("project:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447a44e78877ab1", 1)]
        [InlineData("2ecd0826e447a44e78877ab2", 2)]
        public async Task GetByStackId(string id, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("stack:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("000000000", 0)]
        [InlineData("876554321", 1)]
        public async Task GetByReferenceId(string id, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("reference:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }
        
        [Theory]
        [InlineData("00000", 0)]
        [InlineData("123452366", 1)]
        public async Task GetBySessionId(string id, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("session:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }
        
        [Theory]
        [InlineData("log", 1)]
        [InlineData("error", 2)]
        [InlineData("custom", 0)]
        public async Task GetByType(string type, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("type:" + type);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }
        
        [Theory]
        [InlineData("_missing_:submission", 2)]
        [InlineData("submission:UnobservedTaskException", 1)]
        public async Task GetBySubmissionMethod(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"GET /Print\"", 1)]
        public async Task GetBySource(string source, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("source:" + source);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }
        
        [Theory]
        [InlineData("Error", 1)]
        public async Task GetByLevel(string level, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("level:" + level);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"2014-12-09T17:28:44.966\"", 1)]
        [InlineData("\"2014-12-09T17:28:44.966+00:00\"", 1)]
        [InlineData("\"2015-02-11T20:54:04.3457274+00:00\"", 1)]
        public async Task GetByDate(string date, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("date:" + date);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
        public async Task GetByFirst(bool first, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("first:" + first.ToString().ToLower());
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"Invalid hash. Parameter name: hash\"", 1)] //see what the actual def is for the standard anaylizer
        [InlineData("message:\"Invalid hash. Parameter name: hash\"", 1)]
        public async Task GetByMessage(string filter, int count) {
            await ResetAsync();

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
        public async Task GetByTag(string filter, int count) {
            await ResetAsync();

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
        public async Task GetByValue(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }
        
        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
        public async Task GetByFixed(bool @fixed, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("fixed:" + @fixed.ToString().ToLower());
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }
        
        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
        public async Task GetByHidden(bool hidden, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("hidden:" + hidden.ToString().ToLower());
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("1", 1)]
        [InlineData("1.2", 1)]
        [InlineData("1.2.3", 1)]
        [InlineData("1.2.3.0", 1)]
        public async Task GetByVersion(string version, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("version:" + version);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }
        
        [Theory]
        [InlineData("SERVER-01", 1)]
        [InlineData("machine:SERVER-01", 1)]
        [InlineData("machine:\"SERVER-01\"", 1)]
        public async Task GetByMachine(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("192.168.0.88", 1)]
        [InlineData("ip:192.168.0.88", 1)]
        [InlineData("10.0.0.208", 1)]
        [InlineData("ip:10.0.0.208", 1)]
        public async Task GetByIP(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("x86", 0)]
        [InlineData("x64", 1)]
        public async Task GetByArchitecture(string architecture, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("architecture:" + architecture);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }
        
        [Theory]
        [InlineData("Mozilla", 2)]
        [InlineData("\"Mozilla/5.0\"", 2)]
        [InlineData("5.0", 2)]
        [InlineData("Macintosh", 1)]
        public async Task GetByUserAgent(string userAgent, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("useragent:" + userAgent);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"/user.aspx\"", 1)]
        [InlineData("path:\"/user.aspx\"", 1)]
        public async Task GetByPath(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("browser:Chrome", 2)]
        [InlineData("browser:\"Chrome Mobile\"", 1)]
        [InlineData("browser.raw:\"Chrome Mobile\"", 1)]
        public async Task GetByBrowser(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("browser.version:39.0.2171", 1)]
        [InlineData("browser.version:26.0.1410", 1)]
        [InlineData("browser.version.raw:26.0.1410", 1)]
        public async Task GetByBrowserVersion(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("39", 1)]
        [InlineData("26", 1)]
        public async Task GetByBrowserMajorVersion(string browserMajorVersion, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("browser.major:" + browserMajorVersion);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }
        
        [Theory]
        [InlineData("device:Huawei", 1)]
        [InlineData("device:\"Huawei U8686\"", 1)]
        [InlineData("device.raw:\"Huawei U8686\"", 1)]
        public async Task GetByDevice(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("os:Android", 1)]
        [InlineData("os:Mac", 1)]
        [InlineData("os:\"Mac OS X\"", 1)]
        [InlineData("os.raw:\"Mac OS X\"", 1)]
        [InlineData("os:\"Microsoft Windows Server\"", 1)]
        [InlineData("os:\"Microsoft Windows Server 2012 R2 Standard\"", 1)]
        public async Task GetByOS(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("os.version:4.1.1", 1)]
        [InlineData("os.version:10.10.1", 1)]
        [InlineData("os.version.raw:10.10", 0)]
        [InlineData("os.version.raw:10.10.1", 1)]
        public async Task GetByOSVersion(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("4", 1)]
        [InlineData("10", 1)]
        public async Task GetByOSMajorVersion(string osMajorVersion, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync("os.major:" + osMajorVersion);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }
        
        [Theory]
        [InlineData("bot:false", 1)]
        [InlineData("-bot:true", 2)]
        [InlineData("bot:true", 1)]
        public async Task GetByBot(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result); 
            Assert.Equal(count, result.Total);
        }
        
        [Theory]
        [InlineData("500", 1)]
        [InlineData("error.code:\"-1\"", 1)]
        [InlineData("error.code:500", 1)]
        [InlineData("error.code:5000", 0)]
        public async Task GetByErrorCode(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }
        
        [Theory]
        [InlineData("\"Invalid hash. Parameter name: hash\"", 1)]
        [InlineData("error.message:\"Invalid hash. Parameter name: hash\"", 1)]
        [InlineData("error.message:\"A Task's exception(s)\"", 1)]
        public async Task GetByErrorMessage(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("AssociateWithCurrentThread", 1)]
        [InlineData("error.targetmethod:AssociateWithCurrentThread", 1)]
        public async Task GetByErrorTargetMethod(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("Exception", 2)]
        [InlineData("error.targettype:Exception", 1)]
        [InlineData("error.targettype.raw:System.Exception", 1)]
        public async Task GetByErrorTargetType(string filter, int count) {
            await ResetAsync();

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
        public async Task GetByErrorType(string filter, int count) {
            await ResetAsync();

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
        public async Task GetByUser(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("Blake", 2)] // Matches due to user name and partial tag
        [InlineData("user.name:Blake", 1)]
        public async Task GetByUserName(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("test@exceptionless.com", 1)]
        [InlineData("user.email:test@exceptionless.com", 1)]
        [InlineData("user.email:exceptionless.com", 1)]
        public async Task GetByUserEmailAddress(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Theory]
        [InlineData("\"my custom description\"", 1)]
        [InlineData("user.description:\"my custom description\"", 1)]
        public async Task GetByUserDescription(string filter, int count) {
            await ResetAsync();

            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        //[Theory]
        //[InlineData("\"data.load time-s\":\"262", 1)]
        //public async Task GetByCustomData(string filter, int count) {
        //    var result = await GetByFilterAsync(filter);
        //    Assert.NotNull(result);
        //    Assert.Equal(count, result.Total);
        //}

        private static bool _isReset;

        private async Task ResetAsync() {
            if (!_isReset) {
                _isReset = true;
                await CreateEventsAsync();
            }
        }

        private async Task CreateEventsAsync() {
            _configuration.DeleteIndexes(_client);
            _configuration.ConfigureIndexes(_client);

            var parserPluginManager = IoC.GetInstance<EventParserPluginManager>();
            foreach (var file in Directory.GetFiles(@"..\..\Search\Data\", "event*.json", SearchOption.AllDirectories)) {
                if (file.EndsWith("summary.json"))
                    continue;    

                var events = parserPluginManager.ParseEvents(File.ReadAllText(file), 2, "exceptionless/2.0.0.0");
                Assert.NotNull(events);
                Assert.True(events.Count > 0);
                await _repository.AddAsync(events);
            }

            _client.Refresh();
        }

        private Task<FindResults<PersistentEvent>> GetByFilterAsync(string filter) {
            return _repository.GetByFilterAsync(null, filter, null, SortOrder.Descending, null, DateTime.MinValue, DateTime.MaxValue, new PagingOptions());
        }
    }
}