using Exceptionless.Core.Repositories.Queries;
using Xunit;

namespace Exceptionless.Tests.Search;

public sealed class EventEnvironmentFilterTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("type:error status:open", null)]
    [InlineData("type:error environment:production", "environment:production")]
    [InlineData("environment:production OR type:error", null)]
    [InlineData("type:error (environment:production OR environment:staging)", "(environment:production OR environment:staging)")]
    [InlineData("type:error (_missing_:environment OR environment:production)", "(_missing_:environment OR environment:production)")]
    [InlineData("type:error _exists_:environment", "_exists_:environment")]
    [InlineData("type:error NOT environment:staging", "NOT environment:staging")]
    [InlineData("NOT (environment:staging OR type:error)", "NOT (environment:staging)")]
    [InlineData("NOT (environment:staging AND type:error)", null)]
    public async Task GetAsync_EnvironmentScope_PreservesUsersOutsideTheEventType(string? filter, string? expected)
    {
        Assert.Equal(expected, await EventEnvironmentFilter.GetAsync(filter));
    }
}
