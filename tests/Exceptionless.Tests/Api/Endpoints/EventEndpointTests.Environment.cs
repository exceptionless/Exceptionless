using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Repositories.Models;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public partial class EventEndpointTests
{
    [Theory]
    [InlineData(" Production ", "Production")]
    [InlineData("preview-42", "preview-42")]
    [InlineData("   ", null)]
    [InlineData("bad\nenvironment", null)]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklm", null)]
    public async Task GetSubmitEvent_EnvironmentParameter_TrimsAndPreservesCasing(string environment, string? expected)
    {
        await SendRequestAsync(request => request
            .AsTestOrganizationClientUser().AppendPaths("events", "submit")
            .QueryString("message", "GET environment submission")
            .QueryString("reference", "get-environment-reference")
            .QueryString("environment", environment).StatusCodeShouldBeOk());

        await GetService<EventPostsJob>().RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        var ev = Assert.Single((await _eventRepository.GetAllAsync()).Documents,
            item => item.ReferenceId == "get-environment-reference");
        Assert.Equal(expected, ev.Environment);
        Assert.NotNull(ev.Data);
        Assert.False(ev.Data.ContainsKey("environment"));
    }

    [Fact]
    public async Task GetStacks_EnvironmentFilter_ScopesUserCountsAndTheirCache()
    {
        await CreateDataAsync(data =>
        {
            var first = data.Event().FreeProject().Type(Event.KnownTypes.Error).Mutate(ev => { ev.Environment = "production"; ev.SetUserIdentity("production-0"); });
            for (int i = 1; i < 12; i++)
            {
                string identity = $"production-{i}";
                data.Event().FreeProject().Type(Event.KnownTypes.Error).Stack(first).Mutate(ev => { ev.Environment = "production"; ev.SetUserIdentity(identity); });
            }
            data.Event().FreeProject().Type(Event.KnownTypes.Error).Stack(first).Mutate(ev => { ev.Environment = "staging"; ev.SetUserIdentity("staging-user"); });
            data.Event().FreeProject().Type(Event.KnownTypes.Log).Mutate(ev => { ev.Environment = "production"; ev.SetUserIdentity("unaffected-production-user"); });
            data.Event().FreeProject().Type(Event.KnownTypes.Error).Stack(first).Mutate(ev => { ev.Environment = "development"; ev.SetUserIdentity("development-user"); });
            data.Event().FreeProject().Type(Event.KnownTypes.Log).Mutate(ev => { ev.Environment = "development"; ev.SetUserIdentity("unaffected-development-user"); });
        });

        foreach (var (filter, expected, totalUsers) in new[] { ("environment:production", 12, 13), ("environment:staging", 1, 1), ("environment:production", 12, 13), ("environment:(production OR staging)", 13, 14), ("", 14, 16) })
        {
            var stacks = await SendRequestAsAsync<List<StackSummaryModel>>(request => request
                .AsFreeOrganizationUser().AppendPath("events").QueryString("mode", "stack_frequent")
                .QueryString("filter", $"type:error {filter}").StatusCodeShouldBeOk());
            var stack = Assert.Single(Assert.IsType<List<StackSummaryModel>>(stacks));
            Assert.Equal(expected, stack.Total);
            Assert.Equal(expected, stack.Users);
            Assert.Equal(totalUsers, stack.TotalUsers);
        }
    }

    [Fact]
    public async Task GetEvents_EnvironmentOnFreePlan_FiltersEventsSummariesAndStacks()
    {
        await CreateDataAsync(data =>
        {
            var production = data.Event().FreeProject().Mutate(ev => ev.Environment = " Production ");
            data.Event().FreeProject().Stack(production).Mutate(ev => ev.Environment = "production");
            data.Event().FreeProject().Stack(production).Mutate(ev => ev.Environment = "staging");
            data.Event().FreeProject().Stack(production);
            data.Event().TestProject().Mutate(ev => ev.Environment = "private-environment");
        });

        var events = await SendRequestAsAsync<List<PersistentEvent>>(request => request
            .AsFreeOrganizationUser().AppendPath("events")
            .QueryString("filter", "environment:PRODUCTION").StatusCodeShouldBeOk());
        Assert.Equal(new[] { "Production", "production" }, Assert.IsType<List<PersistentEvent>>(events).Select(ev => ev.Environment).Order(StringComparer.Ordinal).ToArray());

        var summaries = await SendRequestAsAsync<List<EventSummaryModel>>(request => request
            .AsFreeOrganizationUser().AppendPath("events").QueryString("mode", "summary")
            .QueryString("filter", "environment:production").StatusCodeShouldBeOk());
        Assert.Equal(new[] { "Production", "production" }, Assert.IsType<List<EventSummaryModel>>(summaries).Select(ev => ev.Environment).Order(StringComparer.Ordinal).ToArray());

        var stacks = await SendRequestAsAsync<List<StackSummaryModel>>(request => request
            .AsFreeOrganizationUser().AppendPath("events").QueryString("mode", "stack_frequent")
            .QueryString("filter", "environment:production").StatusCodeShouldBeOk());
        Assert.Equal(2, Assert.Single(Assert.IsType<List<StackSummaryModel>>(stacks)).Total);

        var missing = await SendRequestAsAsync<List<PersistentEvent>>(request => request
            .AsFreeOrganizationUser().AppendPath("events")
            .QueryString("filter", "_missing_:environment").StatusCodeShouldBeOk());
        Assert.Null(Assert.Single(Assert.IsType<List<PersistentEvent>>(missing)).Environment);

        var count = await SendRequestAsAsync<CountResult>(request => request
            .AsFreeOrganizationUser().AppendPaths("events", "count")
            .QueryString("aggregations", "terms:(environment~100)").StatusCodeShouldBeOk());
        Assert.NotNull(count);
        Assert.Equal(4, count.Total);
        Assert.Equal(new[] { "production", "staging" }, count.Aggregations.Terms<string>("terms_environment")!.Buckets.Select(bucket => bucket.Key).Order().ToArray());
        Assert.Equal(2, count.Aggregations.Terms<string>("terms_environment")!.Buckets.Single(bucket => bucket.Key == "production").Total);
    }
}
