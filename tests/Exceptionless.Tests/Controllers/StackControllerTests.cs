using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Tests.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Models.Data;
using Exceptionless.Web.Controllers;
using Foundatio.Jobs;
using Foundatio.Queues;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Controllers {
    public class StackControllerTests : IntegrationTestsBase {
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IQueue<EventPost> _eventQueue;
        private readonly IQueue<WorkItemData> _workItemQueue;

        public StackControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
            _stackRepository = GetService<IStackRepository>();
            _eventRepository = GetService<IEventRepository>();
            _eventQueue = GetService<IQueue<EventPost>>();
            _workItemQueue = GetService<IQueue<WorkItemData>>();
        }

        protected override async Task ResetDataAsync() {
            await base.ResetDataAsync();
            await _eventQueue.DeleteQueueAsync();
            await _workItemQueue.DeleteQueueAsync();
            
            var service = GetService<SampleDataService>();
            await service.CreateDataAsync();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("1.0.0")]
        [InlineData("1.0.0.0")]
        public async Task CanMarkFixed(string version) {
            var ev = await SubmitErrorEventAsync();
            Assert.NotNull(ev.StackId);
            Assert.False(ev.IsFixed); 

            var stack = await _stackRepository.GetByIdAsync(ev.StackId);
            Assert.NotNull(stack);
            Assert.False(stack.IsFixed());
            
            await _workItemQueue.DeleteQueueAsync();
            
            var workIds = await SendRequestAsAsync<WorkInProgressResult>(r => r
                .Post()
                .AsGlobalAdminUser()
                .AppendPath($"stacks/{stack.Id}/mark-fixed")
                .QueryStringIf(() => !String.IsNullOrEmpty(version), "version", version)
                .StatusCodeShouldBeAccepted());

            Assert.Single(workIds.Workers);
            
            var stats = await _workItemQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Enqueued);
            Assert.Equal(0, stats.Completed);

            var job = GetService<WorkItemJob>();
            await job.RunAsync();
            await RefreshDataAsync();

            stats = await _workItemQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Completed);
            
            stack = await _stackRepository.GetByIdAsync(ev.StackId);
            Assert.NotNull(stack);
            Assert.True(stack.IsFixed()); 
            
            ev = await _eventRepository.GetByIdAsync(ev.Id);
            Assert.NotNull(ev);
            Assert.True(ev.IsFixed); 
            
            workIds = await SendRequestAsAsync<WorkInProgressResult>(r => r
                .Delete()
                .AsGlobalAdminUser()
                .AppendPath($"stacks/{stack.Id}/mark-fixed")
                .QueryStringIf(() => !String.IsNullOrEmpty(version), "version", version)
                .StatusCodeShouldBeAccepted());
            
            Assert.Single(workIds.Workers);
            
            stack = await _stackRepository.GetByIdAsync(ev.StackId);
            Assert.NotNull(stack);
            Assert.False(stack.IsFixed()); 
            
            await job.RunAsync();
            
            ev = await _eventRepository.GetByIdAsync(ev.Id);
            Assert.NotNull(ev);
            Assert.False(ev.IsFixed); 
        }

        private async Task<PersistentEvent> SubmitErrorEventAsync() {
            const string message = "simple string";
            await SendRequestAsync(r => r
                .Post()
                .AsClientUser()
                .AppendPath("events")
                .Content(new Event {
                    Message = message,
                    Type = Event.KnownTypes.Error,
                    Data = {{ Event.KnownDataKeys.SimpleError, new SimpleError {
                        Message = message,
                        Type = "Error",
                        StackTrace = "test",
                    } }}
                })
                .StatusCodeShouldBeAccepted());

            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Enqueued);
            Assert.Equal(0, stats.Completed);

            var processEventsJob = GetService<EventPostsJob>();
            await processEventsJob.RunAsync();
            await RefreshDataAsync();

            stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Completed);

            var ev = (await _eventRepository.GetAllAsync()).Documents.Single();
            Assert.Equal(message, ev.Message);
            return ev;
        }
    }
}
