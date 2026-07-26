using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models;
using Foundatio.Repositories.Models;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public partial class EventEndpointTests
{
    [Fact]
    public async Task GetAll_WithExplicitFreeOrganizationPremiumFilterAsGlobalAdmin_ReturnsScopedEventsAndCount()
    {
        var (_, events) = await CreateDataAsync(d => d.Event().FreeProject());
        var persistentEvent = Assert.Single(events);
        string filter = $"organization:{SampleDataService.FREE_ORG_ID} id:{persistentEvent.Id}";

        var result = await SendRequestAsAsync<IReadOnlyCollection<PersistentEvent>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("events")
            .QueryString("filter", filter)
            .StatusCodeShouldBeOk());

        var count = await SendRequestAsAsync<CountResult>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("events", "count")
            .QueryString("filter", filter)
            .StatusCodeShouldBeOk());

        Assert.NotNull(result);
        var scopedEvent = Assert.Single(result);
        Assert.Equal(persistentEvent.Id, scopedEvent.Id);
        Assert.NotNull(count);
        Assert.Equal(1, count.Total);
    }

    [Fact]
    public async Task Handle_GetEventCountByProjectWithPremiumAggregationOnFreeOrganization_ReturnsUpgradeRequired()
    {
        await CreateDataAsync(d => d.Event().FreeProject().Tag("premium-tag"));

        await SendRequestAsync(r => r
            .AsFreeOrganizationUser()
            .AppendPaths("projects", SampleDataService.FREE_PROJECT_ID, "events", "count")
            .QueryString("aggregations", "terms:tags")
            .StatusCodeShouldBeUpgradeRequired());
    }

    [Fact]
    public async Task Handle_GetEventCountByProjectWithPremiumFilterOnFreeOrganization_ReturnsUpgradeRequired()
    {
        await CreateDataAsync(d => d.Event().FreeProject().Tag("premium-tag"));

        await SendRequestAsync(r => r
            .AsFreeOrganizationUser()
            .AppendPaths("projects", SampleDataService.FREE_PROJECT_ID, "events", "count")
            .QueryString("filter", "tags:premium-tag")
            .StatusCodeShouldBeUpgradeRequired());
    }

    [Theory]
    [InlineData("critical:false")]
    [InlineData("first_occurrence:[now-1d TO now]")]
    public async Task Handle_GetEventCountByOrganizationInStackModeWithFreeStackFilter_ReturnsOk(string filter)
    {
        await CreateDataAsync(d => d.Event().FreeProject());

        var result = await SendRequestAsAsync<CountResult>(r => r
            .AsFreeOrganizationUser()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "events", "count")
            .QueryString("filter", filter)
            .QueryString("mode", "stack_frequent")
            .StatusCodeShouldBeOk());

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_StackModeWithFreeEventFilters_ReturnsOk()
    {
        var (stacks, _) = await CreateDataAsync(d => d.Event().FreeProject().ReferenceId("free-reference"));
        var stack = Assert.Single(stacks);

        var count = await SendRequestAsAsync<CountResult>(r => r
            .AsFreeOrganizationUser()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "events", "count")
            .QueryString("filter", "reference:free-reference first:true")
            .QueryString("mode", "stack_frequent")
            .StatusCodeShouldBeOk());

        var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
            .AsFreeOrganizationUser()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "events")
            .QueryString("filter", $"stack:{stack.Id} first:true")
            .QueryString("mode", "stack_frequent")
            .StatusCodeShouldBeOk());

        Assert.NotNull(count);
        Assert.Equal(1, count.Total);
        Assert.NotNull(results);
        Assert.Single(results);
    }

    [Fact]
    public async Task Handle_GetEventsByProjectWithPremiumFilterOnFreeOrganization_ReturnsUpgradeRequired()
    {
        await CreateDataAsync(d => d.Event().FreeProject().Tag("premium-tag"));

        await SendRequestAsync(r => r
            .AsFreeOrganizationUser()
            .AppendPaths("projects", SampleDataService.FREE_PROJECT_ID, "events")
            .QueryString("filter", "tags:premium-tag")
            .StatusCodeShouldBeUpgradeRequired());
    }

    [Fact]
    public async Task Handle_GetEventsByProjectInStackModeWithFreeStackFilter_ReturnsOk()
    {
        await CreateDataAsync(d => d.Event().FreeProject());

        var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
            .AsFreeOrganizationUser()
            .AppendPaths("projects", SampleDataService.FREE_PROJECT_ID, "events")
            .QueryString("filter", "critical:false")
            .QueryString("mode", "stack_frequent")
            .StatusCodeShouldBeOk());

        Assert.NotNull(results);
        Assert.Single(results);
    }
}
