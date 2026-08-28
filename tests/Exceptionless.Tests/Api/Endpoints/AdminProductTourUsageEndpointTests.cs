using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models.Admin;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class AdminProductTourUsageEndpointTests : IntegrationTestsBase
{
    private readonly AppOptions _appOptions;

    public AdminProductTourUsageEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _appOptions = GetService<AppOptions>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task GetProductTourUsageAsync_AsGlobalAdmin_ReturnsInternalMonthlyUsage()
    {
        var month = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(month.AddDays(20));

        await CreateDataAsync(builder =>
        {
            AddUsage(builder, "product-tour.shown.ui-overview.v1.automatic", month.AddDays(1), "user-1", 2);
            AddUsage(builder, "product-tour.started.ui-overview.v1.catalog", month.AddDays(2), "user-1");
            AddUsage(builder, "product-tour.started.ui-overview.v1.help-menu", month.AddDays(3), "user-1");
            AddUsage(builder, "product-tour.started.ui-overview.v1.catalog", month.AddDays(4), "user-2");
            AddUsage(builder, "product-tour.completed.ui-overview.v1.catalog", month.AddDays(5), "user-1");
            AddUsage(builder, "product-tour.dismissed.ui-overview.v1.catalog", month.AddDays(6), "user-2");
            AddUsage(builder, "product-tour.started.meet-exie.v1.command-palette", month.AddDays(7), "user-3");
            AddUsage(builder, "product-tour.shown.welcome.v1.automatic", month.AddDays(1), "user-1", 2);
            AddUsage(builder, "product-tour.dismissed.welcome.v1.automatic", month.AddDays(2), "user-1");
            AddUsage(builder, "product-tour.started.unknown.v1.unknown-source", month.AddDays(8), "user-4");

            builder.Event()
                .TestProject()
                .Type(Event.KnownTypes.FeatureUsage)
                .Source("product-tour.started.ignored-tour.v1.catalog")
                .Date(month.AddDays(9))
                .UserIdentity("user-5");
            AddUsage(builder, "product-tour.started.old-tour.v1.catalog", month.AddMonths(-1), "user-6");
        });

        var response = await SendRequestAsAsync<AdminProductTourUsageResponse>(request => request
            .AsGlobalAdminUser()
            .AppendPaths("admin", "product-tour-usage")
            .QueryString("month", "2026-08-01")
            .QueryString("limit", "3")
            .StatusCodeShouldBeOk());

        Assert.NotNull(response);
        Assert.Equal(month, response.Month);
        Assert.Equal(3, response.Tours.Count);

        var overview = Assert.Single(response.Tours, tour => String.Equals(tour.Name, "ui-overview", StringComparison.Ordinal));
        Assert.Equal(2, overview.Shown);
        Assert.Equal(3, overview.Started);
        Assert.Equal(1, overview.Completed);
        Assert.Equal(1, overview.Dismissed);
        Assert.Equal(2, overview.UniqueUsers);
        Assert.Equal(month.AddDays(6), overview.LastRunUtc);
        Assert.Equal(0.3333m, overview.CompletionRate);
        Assert.Equal(0.3333m, overview.DismissalRate);

        var exie = Assert.Single(response.Tours, tour => String.Equals(tour.Name, "meet-exie", StringComparison.Ordinal));
        Assert.Equal(1, exie.Started);
        Assert.Equal(1, exie.UniqueUsers);
        Assert.Equal(month.AddDays(7), exie.LastRunUtc);
        Assert.Equal(0m, exie.CompletionRate);
        Assert.Equal(0m, exie.DismissalRate);

        var welcome = Assert.Single(response.Tours, tour => String.Equals(tour.Name, "welcome", StringComparison.Ordinal));
        Assert.Equal(2, welcome.Shown);
        Assert.Equal(1, welcome.Dismissed);
        Assert.Equal(month.AddDays(2), welcome.LastRunUtc);
        Assert.Equal(0.5m, welcome.DismissalRate);

        Assert.Equal(3, response.RecentActivity.Count);
        Assert.Equal(month.AddDays(7), response.RecentActivity.First().DateUtc);
        Assert.All(response.RecentActivity, activity => Assert.StartsWith("user-", activity.UserIdentity));
    }

    [Fact]
    public async Task GetProductTourUsageAsync_WithoutMonth_UsesCurrentUtcMonth()
    {
        TimeProvider.SetUtcNow(new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc));

        var response = await SendRequestAsAsync<AdminProductTourUsageResponse>(request => request
            .AsGlobalAdminUser()
            .AppendPaths("admin", "product-tour-usage")
            .StatusCodeShouldBeOk());

        Assert.NotNull(response);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), response.Month);
        Assert.Empty(response.Tours);
        Assert.Empty(response.RecentActivity);
    }

    [Fact]
    public Task GetProductTourUsageAsync_AsOrganizationUser_ReturnsForbidden()
    {
        return SendRequestAsync(request => request
            .AsTestOrganizationUser()
            .AppendPaths("admin", "product-tour-usage")
            .StatusCodeShouldBeForbidden());
    }

    private void AddUsage(
        DataBuilder builder,
        string source,
        DateTime dateUtc,
        string userIdentity,
        int count = 1)
    {
        builder.Event()
            .Organization(SampleDataService.TEST_ORG_ID)
            .Project(_appOptions.InternalProjectId)
            .Type(Event.KnownTypes.FeatureUsage)
            .Source(source)
            .Date(dateUtc)
            .UserIdentity(userIdentity, $"Name {userIdentity}")
            .Mutate(ev => ev.Count = count);
    }
}
