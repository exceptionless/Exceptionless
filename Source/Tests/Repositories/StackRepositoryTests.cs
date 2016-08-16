using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using FluentValidation;
using Foundatio.Repositories.Models;
using Xunit;
using Xunit.Abstractions;
using Foundatio.Logging;

namespace Exceptionless.Api.Tests.Repositories {
    public sealed class StackRepositoryTests : ElasticRepositoryTestBase {
        private readonly IStackRepository _repository;

        public StackRepositoryTests(ITestOutputHelper output) : base(output) {
            var eventRepository = new EventRepository(_configuration, IoC.GetInstance<IValidator<PersistentEvent>>(), _cache, null, Log.CreateLogger<EventRepository>());
            Log.SetLogLevel<EventRepository>(LogLevel.Warning);
            _repository = new StackRepository(_configuration, eventRepository, IoC.GetInstance<IValidator<Stack>>(), _cache, null, Log.CreateLogger<StackRepository>());
            Log.SetLogLevel<StackRepository>(LogLevel.Warning);

            RemoveDataAsync().GetAwaiter().GetResult();
        }
        
        [Fact]
        public async Task CanMarkAsRegressedAsync() {
            await _repository.AddAsync(StackData.GenerateStack(id: TestConstants.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, dateFixed: DateTime.Now.SubtractMonths(1)));
            await _client.RefreshAsync();

            var stack = await _repository.GetByIdAsync(TestConstants.StackId);
            Assert.NotNull(stack);
            Assert.False(stack.IsRegressed);
            Assert.NotNull(stack.DateFixed);

            await _repository.MarkAsRegressedAsync(TestConstants.StackId);
            await _client.RefreshAsync();

            stack = await _repository.GetByIdAsync(TestConstants.StackId);
            Assert.NotNull(stack);
            Assert.True(stack.IsRegressed);
            Assert.NotNull(stack.DateFixed);
        }

        [Fact]
        public async Task CanIncrementEventCounterAsync() {
            await _repository.AddAsync(StackData.GenerateStack(id: TestConstants.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId));
            await _client.RefreshAsync();

            var stack = await _repository.GetByIdAsync(TestConstants.StackId);
            Assert.NotNull(stack);
            Assert.Equal(0, stack.TotalOccurrences);
            Assert.Equal(DateTime.MinValue, stack.FirstOccurrence);
            Assert.Equal(DateTime.MinValue, stack.LastOccurrence);

            var utcNow = DateTime.UtcNow;
            await _repository.IncrementEventCounterAsync(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId, utcNow, utcNow, 1);
            await _client.RefreshAsync();

            stack = await _repository.GetByIdAsync(TestConstants.StackId);
            Assert.Equal(1, stack.TotalOccurrences);
            Assert.Equal(utcNow, stack.FirstOccurrence);
            Assert.Equal(utcNow, stack.LastOccurrence);

            await _repository.IncrementEventCounterAsync(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId, utcNow.SubtractDays(1), utcNow.SubtractDays(1), 1);
            await _client.RefreshAsync();

            stack = await _repository.GetByIdAsync(TestConstants.StackId);
            Assert.Equal(2, stack.TotalOccurrences);
            Assert.Equal(utcNow.SubtractDays(1), stack.FirstOccurrence);
            Assert.Equal(utcNow, stack.LastOccurrence);

            await _repository.IncrementEventCounterAsync(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId, utcNow.AddDays(1), utcNow.AddDays(1), 1);
            await _client.RefreshAsync();

            stack = await _repository.GetByIdAsync(TestConstants.StackId);
            Assert.Equal(3, stack.TotalOccurrences);
            Assert.Equal(utcNow.SubtractDays(1), stack.FirstOccurrence);
            Assert.Equal(utcNow.AddDays(1), stack.LastOccurrence);
        }

        [Fact]
        public async Task CanFindManyAsync() {
            Assert.Equal(0, await _repository.CountAsync());

            await _repository.AddAsync(StackData.GenerateSampleStacks());
            await _client.RefreshAsync();

            var stacks = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId, new PagingOptions().WithPage(1).WithLimit(1));
            Assert.NotNull(stacks);
            Assert.Equal(3, stacks.Total);
            Assert.Equal(1, stacks.Documents.Count);

            var stacks2 = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId, new PagingOptions().WithPage(2).WithLimit(1));
            Assert.NotNull(stacks);
            Assert.Equal(1, stacks.Documents.Count);

            Assert.NotEqual(stacks.Documents.First().Id, stacks2.Documents.First().Id);

            stacks = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId);
            Assert.NotNull(stacks);
            Assert.Equal(3, stacks.Documents.Count);

            await _repository.RemoveAsync(stacks.Documents);
            await _client.RefreshAsync();

            Assert.Equal(0, await _repository.CountAsync());
            await _repository.RemoveAllAsync();
            await _client.RefreshAsync();
        }
    }
}