using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Services;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class StackRollupSearchServiceTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Theory]
    [InlineData("date:(date cardinality:stack sum:count~1) cardinality:stack terms:(first @include:true) sum:count~1", true)]
    [InlineData("sum:count~1  terms:(first @include:true) cardinality:stack date:(date sum:count~1 cardinality:stack)", true)]
    [InlineData("sum:count~1\tterms:(first @include:true)\ncardinality:stack date:(date sum:count~1 cardinality:stack)", true)]
    [InlineData("date:(date cardinality:stack sum:count~1) cardinality:stack terms:(first @include:true) sum:count~1 avg:value", false)]
    [InlineData("date:(date cardinality:stack sum:count~1 avg:value) cardinality:stack terms:(first @include:true) sum:count~1", false)]
    [InlineData("date:(date~1d cardinality:stack sum:count~1) cardinality:stack terms:(first @include:true) sum:count~1", false)]
    [InlineData("date:(date cardinality:stack sum:count~10) cardinality:stack terms:(first @include:true) sum:count~1", false)]
    [InlineData("date:(date cardinality:stack sum:count~1) terms:(first @include:true) sum:count~1", false)]
    public async Task IsDashboardAggregation_ValidExpressions_RequiresTheCompleteSupportedShape(string aggregations, bool supported)
    {
        var validation = await GetService<PersistentEventQueryValidator>().ValidateAggregationsAsync(aggregations);

        Assert.True(validation.IsValid, validation.Message);
        Assert.Equal(supported, StackRollupSearchService.IsDashboardAggregation(aggregations));
    }
}
