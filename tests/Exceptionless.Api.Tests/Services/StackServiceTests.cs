using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Utility;
using Nest;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Api.Tests.Services {
    public class StackServiceTests : ElasticTestBase {
        private readonly ICacheClient _cache;
        private readonly StackService _stackService;
        private readonly IStackRepository _stackRepository;

        public StackServiceTests(ITestOutputHelper output) : base(output) {
            Log.SetLogLevel<StackService>(LogLevel.Trace);
            _cache = GetService<ICacheClient>();
            _stackService = GetService<StackService>();
            _stackRepository = GetService<IStackRepository>();
        }

        [Fact]
        public async Task IncrementUsage_OnlyChangeCache() {
            var stack = await _stackRepository.AddAsync(StackData.GenerateStack(id: TestConstants.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId), o => o.ImmediateConsistency());

            // Assert stack state in elasticsearch before increment usage
            Assert.Equal(0, stack.TotalOccurrences);
            Assert.Equal(DateTime.MinValue, stack.FirstOccurrence);
            Assert.Equal(DateTime.MinValue, stack.LastOccurrence);

            // Assert state in cache before increment usage
            Assert.Equal(DateTime.MinValue, await _cache.GetUnixTimeMillisecondsAsync(_stackService.GetStackOccurrenceMinDateCacheKey(stack.Id)));
            Assert.Equal(DateTime.MinValue, await _cache.GetUnixTimeMillisecondsAsync(_stackService.GetStackOccurrenceMaxDateCacheKey(stack.Id)));
            Assert.Equal(0, await _cache.GetAsync<long>(_stackService.GetStackOccurrenceCountCacheKey(stack.Id), 0));
            var occurrenceSet = await _cache.GetSetAsync<(string OrganizationId, string ProjectId, string StackId)>(_stackService.GetStackOccurrenceSetCacheKey());
            Assert.True(occurrenceSet.IsNull || !occurrenceSet.HasValue || occurrenceSet.Value.Count == 0);

            var firstUtcNow = SystemClock.UtcNow.Floor(TimeSpan.FromMilliseconds(1));
            await _configuration.Client.RefreshAsync(Indices.All);
            await _stackService.IncrementStackUsageAsync(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id, firstUtcNow, firstUtcNow, 1);

            // Assert stack state has no change after increment usage
            stack = await _stackRepository.GetByIdAsync(TestConstants.StackId);
            Assert.Equal(0, stack.TotalOccurrences);
            Assert.Equal(DateTime.MinValue, stack.FirstOccurrence);
            Assert.Equal(DateTime.MinValue, stack.LastOccurrence);

            // Assert state in cache has been changed after increment usage
            Assert.Equal(firstUtcNow, await _cache.GetUnixTimeMillisecondsAsync(_stackService.GetStackOccurrenceMinDateCacheKey(stack.Id)));
            Assert.Equal(firstUtcNow, await _cache.GetUnixTimeMillisecondsAsync(_stackService.GetStackOccurrenceMaxDateCacheKey(stack.Id)));
            Assert.Equal(1, await _cache.GetAsync<long>(_stackService.GetStackOccurrenceCountCacheKey(stack.Id), 0));
            occurrenceSet = await _cache.GetSetAsync<(string OrganizationId, string ProjectId, string StackId)>(_stackService.GetStackOccurrenceSetCacheKey());
            Assert.Single(occurrenceSet.Value);

            var secondUtcNow = SystemClock.UtcNow.Floor(TimeSpan.FromMilliseconds(1));
            await _configuration.Client.RefreshAsync(Indices.All);
            await _stackService.IncrementStackUsageAsync(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id, secondUtcNow, secondUtcNow, 2);

            // Assert state in cache has been changed after increment usage again
            Assert.Equal(firstUtcNow, await _cache.GetUnixTimeMillisecondsAsync(_stackService.GetStackOccurrenceMinDateCacheKey(stack.Id)));
            Assert.Equal(secondUtcNow, await _cache.GetUnixTimeMillisecondsAsync(_stackService.GetStackOccurrenceMaxDateCacheKey(stack.Id)));
            Assert.Equal(3, await _cache.GetAsync<long>(_stackService.GetStackOccurrenceCountCacheKey(stack.Id), 0));
            occurrenceSet = await _cache.GetSetAsync<(string OrganizationId, string ProjectId, string StackId)>(_stackService.GetStackOccurrenceSetCacheKey());
            Assert.Single(occurrenceSet.Value);
        }

        [Fact]
        public async Task IncrementUsageConcurrently() {
            var stack = await _stackRepository.AddAsync(StackData.GenerateStack(id: TestConstants.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId));
            var stack2 = await _stackRepository.AddAsync(StackData.GenerateStack(id: TestConstants.StackId2, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId));

            DateTime? minOccurrenceDate = null, maxOccurrenceDate = null;
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++) {
                tasks.Add(IncrementUsageBatch());
            }
            await Task.WhenAll(tasks);

            // Assert stack state has no change after increment usage
            stack = await _stackRepository.GetByIdAsync(TestConstants.StackId);
            Assert.Equal(0, stack.TotalOccurrences);
            Assert.Equal(DateTime.MinValue, stack.FirstOccurrence);
            Assert.Equal(DateTime.MinValue, stack.LastOccurrence);

            // Assert state in cache has been changed after increment usage
            Assert.Equal(minOccurrenceDate, await _cache.GetUnixTimeMillisecondsAsync(_stackService.GetStackOccurrenceMinDateCacheKey(stack.Id)));
            Assert.Equal(maxOccurrenceDate, await _cache.GetUnixTimeMillisecondsAsync(_stackService.GetStackOccurrenceMaxDateCacheKey(stack.Id)));
            Assert.Equal(100, await _cache.GetAsync<long>(_stackService.GetStackOccurrenceCountCacheKey(stack.Id), 0));

            stack2 = await _stackRepository.GetByIdAsync(TestConstants.StackId2);
            Assert.Equal(0, stack2.TotalOccurrences);
            Assert.Equal(DateTime.MinValue, stack2.FirstOccurrence);
            Assert.Equal(DateTime.MinValue, stack2.LastOccurrence);

            // Assert state in cache has been changed after increment usage
            Assert.Equal(minOccurrenceDate, await _cache.GetUnixTimeMillisecondsAsync(_stackService.GetStackOccurrenceMinDateCacheKey(stack2.Id)));
            Assert.Equal(maxOccurrenceDate, await _cache.GetUnixTimeMillisecondsAsync(_stackService.GetStackOccurrenceMaxDateCacheKey(stack2.Id)));
            Assert.Equal(200, await _cache.GetAsync<long>(_stackService.GetStackOccurrenceCountCacheKey(stack2.Id), 0));

            var occurrenceSet = await _cache.GetSetAsync<(string OrganizationId, string ProjectId, string StackId)>(_stackService.GetStackOccurrenceSetCacheKey());
            Assert.Equal(2, occurrenceSet.Value.Count);

            async Task IncrementUsageBatch() {
                for (int i = 0; i < 10; i++) {
                    var utcNow = SystemClock.UtcNow.Floor(TimeSpan.FromMilliseconds(1));
                    if (!minOccurrenceDate.HasValue)
                        minOccurrenceDate = utcNow;
                    maxOccurrenceDate = utcNow;
                    await _stackService.IncrementStackUsageAsync(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id, utcNow, utcNow, 1);
                    await _stackService.IncrementStackUsageAsync(TestConstants.OrganizationId, TestConstants.ProjectId, stack2.Id, utcNow, utcNow, 2);
                }
            }
        }

        [Fact]
        public async Task CanSaveStackUsage() {
            var stack = await _stackRepository.AddAsync(StackData.GenerateStack(id: TestConstants.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId));

            var utcNow = SystemClock.UtcNow.Floor(TimeSpan.FromMilliseconds(1));
            DateTime minOccurrenceDate = utcNow.AddMinutes(-1), maxOccurrenceDate = utcNow;
            await _stackService.IncrementStackUsageAsync(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id, minOccurrenceDate, maxOccurrenceDate, 10);

            await _stackService.SaveStackUsagesAsync(false);

            // Assert state in cache after save stack usage
            Assert.Equal(0, await _cache.GetAsync<long>(_stackService.GetStackOccurrenceCountCacheKey(stack.Id), 0));

            // Assert stack state after save stack usage
            stack = await _stackRepository.GetByIdAsync(TestConstants.StackId);
            Assert.Equal(10, stack.TotalOccurrences);
            Assert.Equal(minOccurrenceDate, stack.FirstOccurrence);
            Assert.Equal(maxOccurrenceDate, stack.LastOccurrence);
        }
    }
}
