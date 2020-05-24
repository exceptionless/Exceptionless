using System;
using System.IO;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Repositories {
    public sealed class StackEventJoinTests : IntegrationTestsBase {
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;

        public StackEventJoinTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
            TestSystemClock.SetFrozenTime(new DateTime(2015, 2, 13, 0, 0, 0, DateTimeKind.Utc));
            _stackRepository = GetService<IStackRepository>();
            _eventRepository = GetService<IEventRepository>();
        }
        
        protected override async Task ResetDataAsync() {
            await base.ResetDataAsync();
            await CreateDataAsync();
        }
        [Theory]
        [InlineData("status:fixed", 2)]
        [InlineData("status:regressed", 1)]
        [InlineData("@stack(status:open)", 1)]
        public async Task GetByStatusAsync(string filter, int count) {
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }
        
        [Theory]
        [InlineData("is_fixed:true", 1)]
        [InlineData("status:fixed", 1)] // Returns 1 because there is two fixed stacks but only one fixed event.
        [InlineData("@stack:(status:fixed)", 1)] // Returns 1 because there is two fixed stacks but only one fixed event.
        [InlineData("tags:old_tag", 0)] // Stack only tags won't be resolved
        [InlineData("type:log status:fixed", 1)]
        [InlineData("type:log version_fixed:1.2.3", 1)]
        [InlineData("type:log is_hidden:false is_fixed:false is_regressed:true", 1)]
        [InlineData("type:log status:fixed @stack:(version_fixed:1.2.3)", 1)]
        [InlineData("54dbc16ca0f5c61398427b00", 1)] // Event Id
        [InlineData("1ecd0826e447a44e78877ab1", 0)] // Stack Id
        [InlineData("type:error", 2)]
        public async Task GetByJoinFilterAsync(string filter, int count) {
            Log.MinimumLevel = LogLevel.Trace;
            var result = await GetByFilterAsync(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }
        
        private Task<FindResults<PersistentEvent>> GetByFilterAsync(string filter) {
            return _eventRepository.GetByFilterAsync(null, filter, null, null, DateTime.MinValue, DateTime.MaxValue);
        }

        private async Task CreateDataAsync() {
            string path = Path.Combine("..", "..", "..", "Search", "Data");
            var serializer = GetService<JsonSerializer>();
            var parserPluginManager = GetService<EventParserPluginManager>();
            foreach (string file in Directory.GetFiles(path, "*.json", SearchOption.AllDirectories)) {
                if (file.EndsWith("summary.json"))
                    continue;

                using (var stream = new FileStream(file, FileMode.Open)) {
                    using (var streamReader = new StreamReader(stream)) {
                        if (file.Contains("event")) {
                            var events = parserPluginManager.ParseEvents(await File.ReadAllTextAsync(file), 2, "exceptionless/2.0.0.0");
                            Assert.NotNull(events);
                            Assert.True(events.Count > 0);
                            foreach (var ev in events)
                                ev.CopyDataToIndex(Array.Empty<string>());

                            await _eventRepository.AddAsync(events, o => o.ImmediateConsistency());
                        } else {
                            var stack = serializer.Deserialize(streamReader, typeof(Stack)) as Stack;
                            Assert.NotNull(stack);
                            await _stackRepository.AddAsync(stack, o => o.ImmediateConsistency());
                        } 
                    }
                }
            }
            
            var configuration = GetService<ExceptionlessElasticConfiguration>();
            configuration.Events.QueryParser.Configuration.RefreshMapping();
        }

    }
}