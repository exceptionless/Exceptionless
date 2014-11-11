using System;
using Exceptionless.Api.Tests.Utility;
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
            RemoveData();
        }

        [Fact]
        public void MarkAsRegressedTest() {
            RemoveData();
            _repository.Add(StackData.GenerateStack(id: TestConstants.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, dateFixed: DateTime.Now.SubtractMonths(1)));
            _client.Refresh();

            var stack = _repository.GetById(TestConstants.StackId);
            Assert.NotNull(stack);
            Assert.False(stack.IsRegressed);
            Assert.NotNull(stack.DateFixed);

            _repository.MarkAsRegressed(TestConstants.StackId);
            
            _client.Refresh();
            stack = _repository.GetById(TestConstants.StackId);
            Assert.NotNull(stack);
            Assert.True(stack.IsRegressed);
            Assert.Null(stack.DateFixed);
        }

        [Fact]
        public void IncrementEventCounterTest() {
            RemoveData();
            _repository.Add(StackData.GenerateStack(id: TestConstants.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId));
            _client.Refresh();

            var stack = _repository.GetById(TestConstants.StackId);
            Assert.NotNull(stack);
            Assert.Equal(0, stack.TotalOccurrences);
            Assert.Equal(DateTime.MinValue, stack.FirstOccurrence);
            Assert.Equal(DateTime.MinValue, stack.LastOccurrence);

            var utcNow = DateTime.UtcNow;
            _repository.IncrementEventCounter(TestConstants.OrganizationId, TestConstants.StackId, utcNow);
            _client.Refresh();

            stack = _repository.GetById(TestConstants.StackId);
            Assert.Equal(1, stack.TotalOccurrences);
            Assert.Equal(utcNow, stack.FirstOccurrence);
            Assert.Equal(utcNow, stack.LastOccurrence);
            
            _repository.IncrementEventCounter(TestConstants.OrganizationId, TestConstants.StackId, utcNow.SubtractDays(1));
            _client.Refresh();

            stack = _repository.GetById(TestConstants.StackId);
            Assert.Equal(2, stack.TotalOccurrences);
            Assert.Equal(utcNow.SubtractDays(1), stack.FirstOccurrence);
            Assert.Equal(utcNow, stack.LastOccurrence);

            _repository.IncrementEventCounter(TestConstants.OrganizationId, TestConstants.StackId, utcNow.AddDays(1));
            _client.Refresh();

            stack = _repository.GetById(TestConstants.StackId);
            Assert.Equal(3, stack.TotalOccurrences);
            Assert.Equal(utcNow.SubtractDays(1), stack.FirstOccurrence);
            Assert.Equal(utcNow.AddDays(1), stack.LastOccurrence);
        }

        [Fact]
        public void GetStackInfoBySignatureHashTest(){}
        
        [Fact]
        public void GetMostRecentTest() { }
        
        [Fact]
        public void GetNewTest() { }
        
        [Fact]
        public void InvalidateCacheTest() { }

        protected void RemoveData() {
            _repository.RemoveAll();
        }
    }
}