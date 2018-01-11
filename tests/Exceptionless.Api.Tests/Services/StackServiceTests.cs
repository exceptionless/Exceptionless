using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
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
            _cache = GetService<ICacheClient>();
            _stackService = GetService<StackService>();
            _stackRepository = GetService<IStackRepository>();
            Log.SetLogLevel<OrganizationRepository>(LogLevel.Information);
        }

        [Fact]
        public async Task IncrementUsage_OnlyChangeCache() {
            var stack = await _stackRepository.AddAsync(StackData.GenerateStack(id: TestConstants.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId));

            // Assert stack state in elasticsearch before increment usage
            Assert.Equal(0, stack.TotalOccurrences);
            Assert.Equal(DateTime.MinValue, stack.FirstOccurrence);
            Assert.Equal(DateTime.MinValue, stack.LastOccurrence);

            // Assert state in cache before increment usage
            Assert.Equal(DateTime.MinValue.Ticks, await _cache.GetAsync<double>(GetStackOccurrenceMinDateCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), DateTime.MinValue.Ticks));
            Assert.Equal(DateTime.MinValue.Ticks, await _cache.GetAsync<double>(GetStackOccurrenceMaxDateCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), DateTime.MinValue.Ticks));
            Assert.Equal(0, await _cache.GetAsync<long>(GetStackOccurrenceCountCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), 0));
            var occurrenceSet = await _cache.GetSetAsync<Tuple<string, string, string>>(GetStackOccurrenceSetCacheKey());
            Assert.True(occurrenceSet.IsNull || !occurrenceSet.HasValue || occurrenceSet.Value.Count == 0);

            var firstUtcNow = SystemClock.UtcNow;
            await _configuration.Client.RefreshAsync(Indices.All);
            await _stackService.IncrementStackUsageAsync(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id, firstUtcNow, firstUtcNow, 1);

            // Assert stack state has no change after increment usage
            stack = await _stackRepository.GetByIdAsync(TestConstants.StackId);
            Assert.Equal(0, stack.TotalOccurrences);
            Assert.Equal(DateTime.MinValue, stack.FirstOccurrence);
            Assert.Equal(DateTime.MinValue, stack.LastOccurrence);

            // Assert state in cache has been changed after increment usage
            Assert.Equal(firstUtcNow.Ticks, await _cache.GetAsync<double>(GetStackOccurrenceMinDateCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), DateTime.MinValue.Ticks));
            Assert.Equal(firstUtcNow.Ticks, await _cache.GetAsync<double>(GetStackOccurrenceMaxDateCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), DateTime.MinValue.Ticks));
            Assert.Equal(1, await _cache.GetAsync<long>(GetStackOccurrenceCountCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), 0));
            occurrenceSet = await _cache.GetSetAsync<Tuple<string, string, string>>(GetStackOccurrenceSetCacheKey());
            Assert.Single(occurrenceSet.Value);

            var secondUtcNow = SystemClock.UtcNow;
            await _configuration.Client.RefreshAsync(Indices.All);
            await _stackService.IncrementStackUsageAsync(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id, secondUtcNow, secondUtcNow, 2);

            // Assert state in cache has been changed after increment usage again
            Assert.Equal(firstUtcNow.Ticks, await _cache.GetAsync<double>(GetStackOccurrenceMinDateCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), DateTime.MinValue.Ticks));
            Assert.Equal(secondUtcNow.Ticks, await _cache.GetAsync<double>(GetStackOccurrenceMaxDateCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), DateTime.MinValue.Ticks));
            Assert.Equal(3, await _cache.GetAsync<long>(GetStackOccurrenceCountCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), 0));
            occurrenceSet = await _cache.GetSetAsync<Tuple<string, string, string>>(GetStackOccurrenceSetCacheKey());
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
            Assert.Equal(minOccurrenceDate.GetValueOrDefault().Ticks, await _cache.GetAsync<double>(GetStackOccurrenceMinDateCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), DateTime.MinValue.Ticks));
            Assert.Equal(maxOccurrenceDate.GetValueOrDefault().Ticks, await _cache.GetAsync<double>(GetStackOccurrenceMaxDateCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), DateTime.MinValue.Ticks));
            Assert.Equal(100, await _cache.GetAsync<long>(GetStackOccurrenceCountCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), 0));

            stack2 = await _stackRepository.GetByIdAsync(TestConstants.StackId2);
            Assert.Equal(0, stack2.TotalOccurrences);
            Assert.Equal(DateTime.MinValue, stack2.FirstOccurrence);
            Assert.Equal(DateTime.MinValue, stack2.LastOccurrence);

            // Assert state in cache has been changed after increment usage
            Assert.Equal(minOccurrenceDate.GetValueOrDefault().Ticks, await _cache.GetAsync<double>(GetStackOccurrenceMinDateCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack2.Id), DateTime.MinValue.Ticks));
            Assert.Equal(maxOccurrenceDate.GetValueOrDefault().Ticks, await _cache.GetAsync<double>(GetStackOccurrenceMaxDateCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack2.Id), DateTime.MinValue.Ticks));
            Assert.Equal(200, await _cache.GetAsync<long>(GetStackOccurrenceCountCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack2.Id), 0));

            var occurrenceSet = await _cache.GetSetAsync<Tuple<string, string, string>>(GetStackOccurrenceSetCacheKey());
            Assert.Equal(2, occurrenceSet.Value.Count);

            async Task IncrementUsageBatch() {
                for (int i = 0; i < 10; i++) {
                    var utcNow = SystemClock.UtcNow;
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
            var stack2 = await _stackRepository.AddAsync(StackData.GenerateStack(id: TestConstants.StackId2, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId));
            var utcNow = SystemClock.UtcNow;
            DateTime minOccurrenceDate = utcNow.AddMinutes(-1), maxOccurrenceDate = utcNow;
            await _stackService.IncrementStackUsageAsync(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id, minOccurrenceDate, maxOccurrenceDate, 10);
            await _stackService.IncrementStackUsageAsync(TestConstants.OrganizationId, TestConstants.ProjectId, stack2.Id, minOccurrenceDate, maxOccurrenceDate, 20);

            await _stackService.SaveStackUsagesAsync(false);

            // Assert state in cache after save stack usage
            Assert.Equal(0, await _cache.GetAsync<long>(GetStackOccurrenceCountCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetStackOccurrenceCountCacheKey(TestConstants.OrganizationId, TestConstants.ProjectId, stack2.Id), 0));

            // Assert stack state after save stack usage
            stack = await _stackRepository.GetByIdAsync(TestConstants.StackId);
            Assert.Equal(10, stack.TotalOccurrences);
            Assert.Equal(minOccurrenceDate, stack.FirstOccurrence);
            Assert.Equal(maxOccurrenceDate, stack.LastOccurrence);

            stack2 = await _stackRepository.GetByIdAsync(TestConstants.StackId2);
            Assert.Equal(20, stack2.TotalOccurrences);
            Assert.Equal(minOccurrenceDate, stack2.FirstOccurrence);
            Assert.Equal(maxOccurrenceDate, stack2.LastOccurrence);

        }

        private string GetStackOccurrenceSetCacheKey() {
            return "usage:occurrences";
        }

        private string GetStackOccurrenceCountCacheKey(string organizationId, string projectId, string stackId) {
            return $"usage:occurrences:count:{organizationId}:{projectId}:{stackId}";
        }

        private string GetStackOccurrenceMinDateCacheKey(string organizationId, string projectId, string stackId) {
            return $"usage:occurrences:mindate:{organizationId}:{projectId}:{stackId}";
        }

        private string GetStackOccurrenceMaxDateCacheKey(string organizationId, string projectId, string stackId) {
            return $"usage:occurrences:maxdate:{organizationId}:{projectId}:{stackId}";
        }
    }
}
