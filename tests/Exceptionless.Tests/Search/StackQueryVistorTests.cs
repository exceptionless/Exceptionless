using Exceptionless.Core.Repositories.Queries;
using Foundatio.Parsers.ElasticQueries;
using Foundatio.Parsers.ElasticQueries.Visitors;
using Foundatio.Parsers.LuceneQueries.Visitors;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Search {
    public class StackQueryVistorTests : TestWithServices {
        public StackQueryVistorTests(ITestOutputHelper output) : base(output) { }

        [Theory]
        [InlineData("status:fixed", "status:fixed", "NOT status:fixed", "")]
        [InlineData("is_fixed:true", "status:fixed", "NOT status:fixed", "")]
        [InlineData("is_regressed:true", "status:regressed", "NOT status:regressed", "")]
        [InlineData("is_hidden:true", "NOT (status:open AND status:regressed)", "(status:open AND status:regressed)", "")]
        [InlineData("is_hidden:false", "(status:open OR status:regressed)", "NOT (status:open OR status:regressed)", "")]
        [InlineData("blah:true (status:fixed OR status:open)", "(status:fixed OR status:open)", "NOT (status:fixed OR status:open)", "blah:true")]
        [InlineData("blah:true", "", "", "blah:true")]
        [InlineData("(organization:123 AND type:log) AND (blah:true (status:fixed OR status:open))", "(organization:123 AND type:log) AND ((status:fixed OR status:open))", "(organization:123 AND type:log) AND (NOT (status:fixed OR status:open))", "(organization:123 AND type:log) AND (blah:true )")]
        [InlineData("project:123 (status:open OR status:regressed) (ref.session:5f3dce2668de920001466635)", "project:123 (status:open OR status:regressed)", "project:123 NOT (status:open OR status:regressed)", "project:123  (ref.session:5f3dce2668de920001466635)")]
        [InlineData("project:123 (status:open OR status:regressed) (ref.session:5f3dce2668de920001466635 OR project:234)", "project:123 (status:open OR status:regressed) (project:234)", "project:123 NOT (status:open OR status:regressed) (project:234)", "project:123  (ref.session:5f3dce2668de920001466635 OR project:234)")]
        public void GetStackQuery(string filter, string expectedStackFilter, string expectedInvertedStackFilter, string expectedEventFilter) {
            var context = new ElasticQueryVisitorContext();

            var stackParser = new ElasticQueryParser(c => {
                c.AddQueryVisitor(new StacksAndEventsQueryVisitor { QueryMode = QueryMode.Stacks });
            });

            var invertedStackParser = new ElasticQueryParser(c => {
                c.AddQueryVisitor(new StacksAndEventsQueryVisitor { QueryMode = QueryMode.InvertedStacks });
            });

            var eventParser = new ElasticQueryParser(c => {
                c.AddQueryVisitor(new StacksAndEventsQueryVisitor { QueryMode = QueryMode.Events });
            });

            var stackResult = stackParser.Parse(filter, context);
            Assert.NotNull(stackResult);

            var invertedStackResult = invertedStackParser.Parse(filter, context);
            Assert.NotNull(stackResult);

            var eventResult = eventParser.Parse(filter, context);
            Assert.NotNull(eventResult);

            var stackFilter = GenerateQueryVisitor.Run(stackResult);
            Assert.Equal(expectedStackFilter, stackFilter.Trim());

            var invertedStackFilter = GenerateQueryVisitor.Run(invertedStackResult);
            Assert.Equal(expectedInvertedStackFilter, invertedStackFilter.Trim());

            var eventFilter = GenerateQueryVisitor.Run(eventResult);
            Assert.Equal(expectedEventFilter, eventFilter.Trim());
        }
    }
}
