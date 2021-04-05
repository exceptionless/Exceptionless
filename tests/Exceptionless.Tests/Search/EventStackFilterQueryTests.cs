using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Tests.Utility;
using Foundatio.AsyncEx;
using Foundatio.Parsers.ElasticQueries.Visitors;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Tests.Search
{
    public class EventStackFilterQueryTests : IntegrationTestsBase {
        private readonly IStackRepository _repository; 
        private static bool _isReset;

        public EventStackFilterQueryTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
            _repository = GetService<IStackRepository>();
        }

        protected override async Task ResetDataAsync() {
            if (_isReset)
                return;

            await base.ResetDataAsync();
            await CreateDataAsync(d => {
                for (int index = 0; index < 13000; index++) {
                    var status = index switch {
                        var i when i < 3000 => StackStatus.Fixed,
                        var i when i < 4000 => StackStatus.Ignored,
                        var i when i < 10000 => StackStatus.Discarded,
                        var i when i < 12000 => StackStatus.Regressed,
                        _ => StackStatus.Open
                    };

                    var builder = d.Event().Type(Event.KnownTypes.Log).Status(status)
                        .Source($"Exceptionless.Test.{index}").Value(index);
                    if (index == 0)
                        builder.StackId(TestConstants.StackId);

                    if (index % 100 == 0)
                        builder.SessionId("sessionId");

                    if (index < 1000)
                        builder.Deleted();
                }
            });
            
            _isReset = true;
        }
        
        [Theory]
        [InlineData("-type:heartbeat (reference:sessionId OR ref.session:sessionId)", 13000)]
        [InlineData("status:open OR status:regressed", 3000)]
        [InlineData("NOT (status:open OR status:regressed)", 10000)]
        [InlineData("status:fixed", 3000)]
        [InlineData("NOT status:fixed", 10000)]
        [InlineData("stack:" + TestConstants.StackId, 1)]
        [InlineData("-stack:" + TestConstants.StackId, 12999)]
        [InlineData("stack:" + TestConstants.StackId + " (status:open OR status:regressed)", 0)]
        public async Task VerifyEventStackFilter(string filter, long total) {
            Log.SetLogLevel<StackRepository>(LogLevel.Trace);
            const int stackIdLimit = 10000;

            var ctx = new ElasticQueryVisitorContext();
            var stackFilter = await new EventStackFilter().GetStackFilterAsync(filter, ctx);
            _logger.LogInformation("Finding Filter: {Filter}", stackFilter.Filter);
            var stacks = await _repository.GetIdsByQueryAsync(q => q.FilterExpression(stackFilter.Filter), o => o.PageLimit(stackIdLimit).SoftDeleteMode(SoftDeleteQueryMode.All));
            Assert.Equal(total, stacks.Total);

            // non-inverted conditions (project, org, stack) must be at beginning of filter
            // invert will validate this and then just wrap the rest of the query in a NOT (<original>)

            _logger.LogInformation("Finding Inverted Filter: {Filter}", stackFilter.InvertedFilter);
            var invertedStacks = await _repository.GetIdsByQueryAsync(q => q.FilterExpression(stackFilter.InvertedFilter), o => o.PageLimit(stackIdLimit).SoftDeleteMode(SoftDeleteQueryMode.All));
            Assert.Equal(13000 - total, invertedStacks.Total);

            var stackIds = new HashSet<string>(stacks.Hits.Select(h => h.Id));
            var invertedStackIds = new HashSet<string>(invertedStacks.Hits.Select(h => h.Id));

            Assert.Equal(total, stackIds.Except(invertedStackIds).Count());
        }
    }
}