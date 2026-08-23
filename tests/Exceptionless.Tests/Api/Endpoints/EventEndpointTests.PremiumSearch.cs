using Exceptionless.Core.Models;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Models;
using Foundatio.Repositories.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public partial class EventEndpointTests
{
    [Fact]
    public async Task GetAll_WithExplicitFreeOrganizationPremiumFilterAsGlobalAdmin_ReturnsScopedEventsAndCount()
    {
        // Arrange
        var (_, events) = await CreateDataAsync(d => d.Event().FreeProject());
        var persistentEvent = Assert.Single(events);
        string filter = $"organization:{SampleDataService.FREE_ORG_ID} id:{persistentEvent.Id}";

        // Act
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

        // Assert
        Assert.NotNull(result);
        var scopedEvent = Assert.Single(result);
        Assert.Equal(persistentEvent.Id, scopedEvent.Id);
        Assert.NotNull(count);
        Assert.Equal(1, count.Total);
    }

    [Theory]
    [InlineData("critical:false")]
    [InlineData("first_occurrence:[now-1d TO now]")]
    public async Task Handle_GetStackRollupStatsByOrganizationWithFreeStackFilter_ReturnsOk(string filter)
    {
        // Arrange
        await CreateDataAsync(d => d.Event().FreeProject());

        // Act
        var result = await SendRequestAsAsync<CountResult>(r => r
            .AsFreeOrganizationUser()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "events", "count")
            .QueryString("aggregations", "date:(date cardinality:stack sum:count~1) cardinality:stack terms:(first @include:true) sum:count~1")
            .QueryString("mode", "stack")
            .QueryString("filter", filter)
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_GetEventCountByProjectWithPremiumAggregationOnFreeOrganization_ReturnsUpgradeRequired()
    {
        // Arrange
        await CreateDataAsync(d => d.Event().FreeProject().Tag("premium-tag"));

        // Act
        var problemDetails = await SendRequestAsAsync<ProblemDetails>(r => r
            .AsFreeOrganizationUser()
            .AppendPaths("projects", SampleDataService.FREE_PROJECT_ID, "events", "count")
            .QueryString("aggregations", "terms:tags")
            .StatusCodeShouldBeUpgradeRequired());

        // Assert
        AssertUpgradeRequired(problemDetails, ApiFilterPolicy.PremiumSearchUpgradeMessage);
    }

    [Fact]
    public async Task Handle_GetEventCountByProjectWithPremiumFilterOnFreeOrganization_ReturnsUpgradeRequired()
    {
        // Arrange
        await CreateDataAsync(d => d.Event().FreeProject().Tag("premium-tag"));

        // Act
        var problemDetails = await SendRequestAsAsync<ProblemDetails>(r => r
            .AsFreeOrganizationUser()
            .AppendPaths("projects", SampleDataService.FREE_PROJECT_ID, "events", "count")
            .QueryString("filter", "tags:premium-tag")
            .StatusCodeShouldBeUpgradeRequired());

        // Assert
        AssertUpgradeRequired(problemDetails, ApiFilterPolicy.PremiumSearchUpgradeMessage);
    }

    [Fact]
    public async Task Handle_GetStackModeEventsByProjectWithFreeStackFilter_ReturnsOk()
    {
        // Arrange
        await CreateDataAsync(d => d.Event().FreeProject());

        // Act
        var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
            .AsFreeOrganizationUser()
            .AppendPaths("projects", SampleDataService.FREE_PROJECT_ID, "events")
            .QueryString("mode", "stack")
            .QueryString("filter", "critical:false")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(results);
        Assert.Single(results);
    }

    [Fact]
    public async Task Handle_GetEventsByProjectWithPremiumFilterOnFreeOrganization_ReturnsUpgradeRequired()
    {
        // Arrange
        await CreateDataAsync(d => d.Event().FreeProject().Tag("premium-tag"));

        // Act
        var problemDetails = await SendRequestAsAsync<ProblemDetails>(r => r
            .AsFreeOrganizationUser()
            .AppendPaths("projects", SampleDataService.FREE_PROJECT_ID, "events")
            .QueryString("filter", "tags:premium-tag")
            .StatusCodeShouldBeUpgradeRequired());

        // Assert
        AssertUpgradeRequired(problemDetails, ApiFilterPolicy.PremiumSearchUpgradeMessage);
    }

    [Fact]
    public async Task Handle_GetSessionsByOrganizationOnFreeOrganization_ReturnsUpgradeRequired()
    {
        // Arrange
        await CreateDataAsync(d => d.Event().FreeProject().Type(Event.KnownTypes.Session));

        // Act
        var problemDetails = await SendRequestAsAsync<ProblemDetails>(r => r
            .AsFreeOrganizationUser()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "events", "sessions")
            .StatusCodeShouldBeUpgradeRequired());

        // Assert
        AssertUpgradeRequired(problemDetails, ApiFilterPolicy.PremiumSessionUpgradeMessage);
    }

    [Fact]
    public async Task Handle_StackRollupsWithFreeEventFilters_ReturnsOk()
    {
        // Arrange
        var (stacks, _) = await CreateDataAsync(d => d.Event().FreeProject().ReferenceId("free-reference"));
        var stack = Assert.Single(stacks);

        // Act
        var stats = await SendRequestAsAsync<CountResult>(r => r
            .AsFreeOrganizationUser()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "events", "count")
            .QueryString("aggregations", "date:(date cardinality:stack sum:count~1) cardinality:stack terms:(first @include:true) sum:count~1")
            .QueryString("mode", "stack")
            .QueryString("filter", "reference:free-reference first:true")
            .StatusCodeShouldBeOk());

        var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
            .AsFreeOrganizationUser()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "events")
            .QueryString("mode", "stack")
            .QueryString("filter", $"stack:{stack.Id} first:true")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(1, stats.Aggregations.Sum("sum_count")?.Value);
        Assert.NotNull(results);
        Assert.Single(results);
    }

    private static void AssertUpgradeRequired(ProblemDetails? problemDetails, string expectedTitle)
    {
        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status426UpgradeRequired, problemDetails.Status);
        Assert.Equal(expectedTitle, problemDetails.Title);
    }
}
