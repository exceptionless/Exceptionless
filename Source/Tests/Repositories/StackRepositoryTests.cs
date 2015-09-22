using System;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Component;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Nest;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class StackRepositoryTests {
        private const int NUMBER_OF_STACKS_TO_CREATE = 50;
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private readonly IStackRepository _repository = IoC.GetInstance<IStackRepository>();

        public StackRepositoryTests() {
            RemoveDataAsync().AnyContext().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task MarkAsRegressedTest() {
            await RemoveDataAsync().AnyContext();
            await _repository.AddAsync(StackData.GenerateStack(id: TestConstants.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, dateFixed: DateTime.Now.SubtractMonths(1))).AnyContext();
            _client.Refresh();

            var stack = await _repository.GetByIdAsync(TestConstants.StackId).AnyContext();
            Assert.NotNull(stack);
            Assert.False(stack.IsRegressed);
            Assert.NotNull(stack.DateFixed);

            await _repository.MarkAsRegressedAsync(TestConstants.StackId).AnyContext();
            
            _client.Refresh();
            stack = await _repository.GetByIdAsync(TestConstants.StackId).AnyContext();
            Assert.NotNull(stack);
            Assert.True(stack.IsRegressed);
            Assert.Null(stack.DateFixed);
        }

        [Fact]
        public async Task IncrementEventCounterTest() {
            await RemoveDataAsync().AnyContext();
            await _repository.AddAsync(StackData.GenerateStack(id: TestConstants.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId)).AnyContext();
            _client.Refresh();

            var stack = await _repository.GetByIdAsync(TestConstants.StackId).AnyContext();
            Assert.NotNull(stack);
            Assert.Equal(0, stack.TotalOccurrences);
            Assert.Equal(DateTime.MinValue, stack.FirstOccurrence);
            Assert.Equal(DateTime.MinValue, stack.LastOccurrence);

            var utcNow = DateTime.UtcNow;
            await _repository.IncrementEventCounterAsync(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId, utcNow, utcNow, 1).AnyContext();
            _client.Refresh();

            stack = await _repository.GetByIdAsync(TestConstants.StackId).AnyContext();
            Assert.Equal(1, stack.TotalOccurrences);
            Assert.Equal(utcNow, stack.FirstOccurrence);
            Assert.Equal(utcNow, stack.LastOccurrence);

            await _repository.IncrementEventCounterAsync(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId, utcNow.SubtractDays(1), utcNow.SubtractDays(1), 1).AnyContext();
            _client.Refresh();

            stack = await _repository.GetByIdAsync(TestConstants.StackId).AnyContext();
            Assert.Equal(2, stack.TotalOccurrences);
            Assert.Equal(utcNow.SubtractDays(1), stack.FirstOccurrence);
            Assert.Equal(utcNow, stack.LastOccurrence);

            await _repository.IncrementEventCounterAsync(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId, utcNow.AddDays(1), utcNow.AddDays(1), 1).AnyContext();
            _client.Refresh();

            stack = await _repository.GetByIdAsync(TestConstants.StackId).AnyContext();
            Assert.Equal(3, stack.TotalOccurrences);
            Assert.Equal(utcNow.SubtractDays(1), stack.FirstOccurrence);
            Assert.Equal(utcNow.AddDays(1), stack.LastOccurrence);
        }

        [Fact]
        public Task GetStackInfoBySignatureHashTest() {
            return TaskHelper.Completed();
        }
        
        [Fact]
        public Task GetMostRecentTest() {
            return TaskHelper.Completed();
        }
        
        [Fact]
        public Task GetNewTest() {
            return TaskHelper.Completed();
        }
        
        [Fact]
        public Task InvalidateCacheTest() {
            return TaskHelper.Completed();
        }

        protected async Task RemoveDataAsync() {
            await _repository.RemoveAllAsync().AnyContext();
        }
    }
}