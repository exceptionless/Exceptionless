using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Parsers.ElasticQueries;
using Foundatio.Parsers.ElasticQueries.Visitors;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Search {
    public class PersistentEventQueryValidatorTests : TestBase {
        private readonly ElasticQueryParser _parser;
        private readonly PersistentEventQueryValidator _validator;

        public PersistentEventQueryValidatorTests(ITestOutputHelper output) : base(output) {
            _parser = GetService<ExceptionlessElasticConfiguration>().Events.Event.QueryParser;
            _validator = GetService<PersistentEventQueryValidator>();
        }

        [Theory]
        [InlineData("data.@user.identity:blake", "data.@user.identity:blake", true, true)]
        [InlineData("data.user.identity:blake", "data.user.identity:blake", true, true)]
        [InlineData("_missing_:data.sessionend", "_missing_:idx.sessionend-d", true, true)]
        [InlineData("data.SessionEnd:<now", "idx.sessionend-d:<now", true, true)]
        [InlineData("data.haserror:true", "idx.haserror-b:true", true, true)]
        [InlineData("data.field:(now criteria2)", "idx.field-s:(now criteria2)", true, true)]
        [InlineData("data.date:>now", "idx.date-d:>now", true, true)]
        [InlineData("data.date:[now/d-4d TO now/d+1d}", "idx.date-d:[now/d-4d TO now/d+1d}", true, true)]
        [InlineData("data.date:[2012-01-01 TO 2012-12-31]", "idx.date-d:[2012-01-01 TO 2012-12-31]", true, true)]
        [InlineData("data.date:[* TO 2012-12-31]", "idx.date-d:[* TO 2012-12-31]", true, true)]
        [InlineData("data.date:[2012-01-01 TO *]", "idx.date-d:[2012-01-01 TO *]", true, true)]
        [InlineData("(data.date:[now/d-4d TO now/d+1d})", "(idx.date-d:[now/d-4d TO now/d+1d})", true, true)]
        [InlineData("data.count:[1..5}", "idx.count-n:[1..5}", true, true)]
        [InlineData("data.Windows-identity:ejsmith", "idx.windows-identity-s:ejsmith", true, true)]
        [InlineData("data.age:(>30 AND <=40)", "idx.age-n:(>30 AND <=40)", true, true)]
        [InlineData("data.age:(+>=10 AND < 20)", "idx.age-n:(+>=10 AND <20)", true, true)]
        [InlineData("data.age:(+>=10 +<20)", "idx.age-n:(+>=10 +<20)", true, true)]
        [InlineData("data.age:(->=10 AND < 20)", "idx.age-n:(->=10 AND <20)", true, true)]
        [InlineData("data.age:[10 TO *]", "idx.age-n:[10 TO *]", true, true)]
        [InlineData("data.age:[* TO 10]", "idx.age-n:[* TO 10]", true, true)]
        [InlineData("hidden:true AND data.age:(>30 AND <=40)", "is_hidden:true AND idx.age-n:(>30 AND <=40)", true, true)]
        [InlineData("hidden:true", "is_hidden:true", true, false)]
        [InlineData("fixed:true", "is_fixed:true", true, false)]
        [InlineData("type:404", "type:404", true, false)]
        [InlineData("reference:404", "reference_id:404", true, false)]
        [InlineData("organization:404", "organization_id:404", true, false)]
        [InlineData("project:404", "project_id:404", true, false)]
        [InlineData("stack:404", "stack_id:404", true, false)]
        [InlineData("ref.session:12345678", "idx.session-r:12345678", true, true)]
        public async Task CanProcessQueryAsync(string query, string expected, bool isValid, bool usesPremiumFeatures) {
            var context = new ElasticQueryVisitorContext();

            IQueryNode result;
            try {
                result = await _parser.ParseAsync(query, QueryType.Query, context).AnyContext();
            } catch (Exception ex) {
                _logger.LogError(ex, "Error parsing query: {Query}. Message: {Message}", query, ex.Message);
                if (isValid)
                    throw;

                return;
            }

            // NOTE: we have to do this because we don't have access to the right query parser instance.
            result = await EventFieldsQueryVisitor.RunAsync(result, context);
            Assert.Equal(expected, await GenerateQueryVisitor.RunAsync(result, context));

            var info = await _validator.ValidateQueryAsync(result);
            _logger.LogInformation("UsesPremiumFeatures: {UsesPremiumFeatures} IsValid: {IsValid} Message: {Message}", info.UsesPremiumFeatures, info.IsValid, info.Message);
            Assert.Equal(isValid, info.IsValid);
            Assert.Equal(usesPremiumFeatures, info.UsesPremiumFeatures);
        }

        [Theory]
        [InlineData(null, true, false)]
        [InlineData("avg", false, false)]
        [InlineData("avg:", false, false)]
        [InlineData("avg:val", false, true)]
        [InlineData("avg:value", true, false)]
        [InlineData("max:date", true, false)]
        [InlineData("avg:count", true, false)]
        [InlineData("terms:(first @include:true)", true, false)]
        [InlineData("cardinality:stack", true, false)]
        [InlineData("cardinality:user", true, false)]
        [InlineData("cardinality:type", true, false)]
        [InlineData("cardinality:source", true, true)]
        [InlineData("cardinality:tags", true, true)]
        [InlineData("cardinality:geo", true, true)]
        [InlineData("cardinality:fixed", true, true)]
        [InlineData("cardinality:hidden", true, true)]
        [InlineData("cardinality:organization", true, true)]
        [InlineData("cardinality:project", true, true)]
        [InlineData("cardinality:error.code", true, true)]
        [InlineData("cardinality:error.type", true, true)]
        [InlineData("cardinality:error.targettype", true, true)]
        [InlineData("cardinality:error.targetmethod", true, true)]
        [InlineData("cardinality:machine", true, true)]
        [InlineData("cardinality:architecture", true, true)]
        [InlineData("cardinality:country", true, true)]
        [InlineData("cardinality:level1", true, true)]
        [InlineData("cardinality:level2", true, true)]
        [InlineData("cardinality:locality", true, true)]
        [InlineData("cardinality:browser", true, true)]
        [InlineData("cardinality:browser.major", true, true)]
        [InlineData("cardinality:device", true, true)]
        [InlineData("cardinality:os", true, true)]
        [InlineData("cardinality:os.version", true, true)]
        [InlineData("cardinality:os.major", true, true)]
        [InlineData("cardinality:bot", true, true)]
        [InlineData("cardinality:version", true, true)]
        [InlineData("date:(date cardinality:stack sum:count~1) cardinality:stack terms:(first @include:true) sum:count~1", true, false)] // dashboards
        [InlineData("date:(date cardinality:user sum:value avg:value sum:count~1) min:date max:date cardinality:user sum:count~1", true, false)] // stack dashboard
        [InlineData("avg:value cardinality:user date:(date cardinality:user)", true, false)] // session dashboard
        [InlineData("date:(date~month terms:(project cardinality:stack terms:(first @include:true)) cardinality:stack terms:(first @include:true))", true, true)] // Breakdown of total events, new events and unique events per month by project
        public async Task CanProcessAggregationsAsync(string query, bool isValid, bool usesPremiumFeatures) {
            var info = await _validator.ValidateAggregationsAsync(query);
            _logger.LogInformation("UsesPremiumFeatures: {UsesPremiumFeatures} IsValid: {IsValid} Message: {Message}", info.UsesPremiumFeatures, info.IsValid, info.Message);
            Assert.Equal(isValid, info.IsValid);
            if (isValid)
                Assert.Equal(usesPremiumFeatures, info.UsesPremiumFeatures);
        }
    }
}