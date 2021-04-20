using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Foundatio.Parsers.ElasticQueries.Visitors;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Search {
    public class EventStackFilterQueryTests : IntegrationTestsBase {
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;
        private static bool _isTestDataGenerated;

        public EventStackFilterQueryTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
            _stackRepository = GetService<IStackRepository>();
            _eventRepository = GetService<IEventRepository>();
        }

        protected override async Task ResetDataAsync() {
            if (_isTestDataGenerated)
                return;

            await base.ResetDataAsync();
            await CreateDataAsync(d => {
                d.Event().Type(Event.KnownTypes.Log).Status(StackStatus.Open).Source("Namespace.ClassName").UserIdentity("test@test.com", "Test Test");
                d.Event().Type(Event.KnownTypes.Log).FreeProject().Status(StackStatus.Open);
                d.Event().StackId(TestConstants.StackId).Type(Event.KnownTypes.Log).Status(StackStatus.Open);
                d.Event().Type(Event.KnownTypes.Log).Status(StackStatus.Open).Deleted();

                d.Event().Type(Event.KnownTypes.Log).Status(StackStatus.Fixed);

                d.Event().Type(Event.KnownTypes.Log).Status(StackStatus.Ignored);
                d.Event().Type(Event.KnownTypes.Session).Status(StackStatus.Ignored);
                d.Event().Type(Event.KnownTypes.Log).Status(StackStatus.Open).ReferenceId("referenceId");
                d.Event().Type(Event.KnownTypes.Log).Status(StackStatus.Open).SessionId("sessionId");

                d.Event().Type(Event.KnownTypes.Log).Status(StackStatus.Discarded);
                d.Event().Type(Event.KnownTypes.Log).Status(StackStatus.Regressed);
            });
            
            _isTestDataGenerated = true;
        }
        
        [Theory]
        [InlineData("status:open OR status:regressed", 7)]
        [InlineData("NOT (status:open OR status:regressed)", 4)]
        [InlineData("status:fixed", 1)]
        [InlineData("NOT status:fixed", 10)]
        [InlineData("stack:" + TestConstants.StackId, 1)]
        [InlineData("stack_id:" + TestConstants.StackId, 1)]
        [InlineData("-stack:" + TestConstants.StackId, 10)]
        [InlineData("stack:" + TestConstants.StackId + " (status:open OR status:regressed)", 1)]
        [InlineData("is_fixed:true", 1)]
        [InlineData("is_regressed:true", 1)]
        [InlineData("is_hidden:true", 4)]
        [InlineData("project:" + SampleDataService.TEST_PROJECT_ID + " (status:open OR status:regressed)", 6, 4)]
        public async Task VerifyStackFilter(string filter, int expected, int? expectedInverted = null) {
            Log.SetLogLevel<StackRepository>(LogLevel.Trace);

            long totalStacks = await _stackRepository.CountAsync(o => o.IncludeSoftDeletes());

            var ctx = new ElasticQueryVisitorContext();
            var stackFilter = await new EventStackFilter().GetStackFilterAsync(filter, ctx);
            _logger.LogInformation("Finding Filter: {Filter}", stackFilter.Filter);
            var stacks = await _stackRepository.FindAsync(q => q.FilterExpression(stackFilter.Filter), o => o.SoftDeleteMode(SoftDeleteQueryMode.All).PageLimit(1000));
            Assert.Equal(expected, stacks.Total);

            _logger.LogInformation("Finding Inverted Filter: {Filter}", stackFilter.InvertedFilter);
            var invertedStacks = await _stackRepository.FindAsync(q => q.FilterExpression(stackFilter.InvertedFilter), o => o.SoftDeleteMode(SoftDeleteQueryMode.All).PageLimit(1000));
            long expectedInvert = expectedInverted ?? totalStacks - expected;
            Assert.Equal(expectedInvert, invertedStacks.Total);

            var stackIds = new HashSet<string>(stacks.Hits.Select(h => h.Id));
            var invertedStackIds = new HashSet<string>(invertedStacks.Hits.Select(h => h.Id));

            Assert.Empty(stackIds.Intersect(invertedStackIds));
        }

        [Theory]
        [InlineData("status:open OR status:regressed", 6)]
        [InlineData("NOT (status:open OR status:regressed)", 4)]
        [InlineData("status:fixed", 1)]
        [InlineData("NOT status:fixed", 9)]
        [InlineData("stack:" + TestConstants.StackId, 1)]
        [InlineData("stack_id:" + TestConstants.StackId, 1)]
        [InlineData("-stack:" + TestConstants.StackId, 9)]
        [InlineData("stack:" + TestConstants.StackId + " (status:open OR status:regressed)", 1)]
        [InlineData("is_fixed:true", 1)]
        [InlineData("is_regressed:true", 1)]
        [InlineData("is_hidden:true", 4)]
        [InlineData("project:" + SampleDataService.TEST_PROJECT_ID + " (status:open OR status:regressed)", 5)]
        [InlineData("project:" + SampleDataService.TEST_PROJECT_ID + " (status:open OR status:regressed) ref.session:sessionId", 1)]
        [InlineData("project:" + SampleDataService.TEST_PROJECT_ID + " (status:open OR status:regressed) reference:referenceId", 1)]
        public async Task VerifyEventFilter(string filter, int expected) {
            Log.SetLogLevel<StackRepository>(LogLevel.Trace);
            Log.SetLogLevel<EventRepository>(LogLevel.Trace);
            Log.SetLogLevel<EventStackFilterQueryBuilder>(LogLevel.Trace);

            var events = await _eventRepository.FindAsync(q => q.FilterExpression(filter).EnforceEventStackFilter(), o => o.PageLimit(1000));
            Assert.Equal(expected, events.Total);

            var invertedEvents = await _eventRepository.FindAsync(q => q.FilterExpression("@!" + filter).EnforceEventStackFilter(), o => o.PageLimit(1000));
            Assert.Equal(expected, invertedEvents.Total);
        }
    }
}