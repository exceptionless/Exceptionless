using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
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
        // Arrange
        var month = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(month.AddDays(20));

        await CreateDataAsync(builder =>
        {
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog), month.AddDays(2), "user-1");
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.HelpMenu), month.AddDays(3), "user-1");
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog), month.AddDays(4), "user-2");
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Completed, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog), month.AddDays(5), "user-1");
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Dismissed, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog), month.AddDays(6), "user-2");
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.ExieOverview, 1, ProductTourLaunchSource.CommandPalette), month.AddDays(7), "user-3");
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Shown, ProductTours.AppWelcome, 1, ProductTourLaunchSource.Automatic), month.AddDays(1), "user-1", 2);
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppWelcome, 1, ProductTourLaunchSource.Automatic), month.AddDays(1).AddHours(1), "user-1");
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Completed, ProductTours.AppWelcome, 1, ProductTourLaunchSource.Automatic), month.AddDays(1).AddHours(2), "user-1");
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Dismissed, ProductTours.AppWelcome, 1, ProductTourLaunchSource.Automatic), month.AddDays(2), "user-1");
            AddUsage(builder, "product-tour.started.unknown.v1.unknown-source", month.AddDays(8), "user-4");

            builder.Event()
                .TestProject()
                .Type(Event.KnownTypes.FeatureUsage)
                .Source("product-tour.started.ignored-tour.v1.catalog")
                .Date(month.AddDays(9))
                .UserIdentity("user-5");
            AddUsage(builder, "product-tour.started.old-tour.v1.catalog", month.AddMonths(-1), "user-6");
        });

        // Act
        var response = await SendRequestAsAsync<ProductTourUsageResponse>(request => request
            .AsGlobalAdminUser()
            .AppendPaths("admin", "product-tour-usage")
            .QueryString("month", "2026-08-01")
            .QueryString("limit", "3")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(response);
        Assert.Equal(month, response.Month);
        Assert.Equal(3, response.Tours.Count);

        var overview = Assert.Single(response.Tours, tour => String.Equals(tour.Name, ProductTours.AppOverview, StringComparison.Ordinal));
        Assert.Equal(0, overview.Shown);
        Assert.Equal(3, overview.Started);
        Assert.Equal(3, overview.ManualStarted);
        Assert.Equal(1, overview.Completed);
        Assert.Equal(1, overview.Dismissed);
        Assert.Equal(month.AddDays(6), overview.LastRunUtc);
        Assert.Null(overview.StartedRate);
        Assert.Equal(1m, overview.ManualStartedRate);
        Assert.Equal(0.3333m, overview.CompletionRate);
        Assert.Equal(0.3333m, overview.DismissalRate);

        var exie = Assert.Single(response.Tours, tour => String.Equals(tour.Name, ProductTours.ExieOverview, StringComparison.Ordinal));
        Assert.Equal(1, exie.Started);
        Assert.Equal(1, exie.ManualStarted);
        Assert.Null(exie.StartedRate);
        Assert.Equal(1m, exie.ManualStartedRate);
        Assert.Equal(month.AddDays(7), exie.LastRunUtc);
        Assert.Equal(0m, exie.CompletionRate);
        Assert.Equal(0m, exie.DismissalRate);

        var welcome = Assert.Single(response.Tours, tour => String.Equals(tour.Name, ProductTours.AppWelcome, StringComparison.Ordinal));
        Assert.Equal(2, welcome.Shown);
        Assert.Equal(1, welcome.Started);
        Assert.Equal(0.5m, welcome.StartedRate);
        Assert.Equal(0m, welcome.ManualStartedRate);
        Assert.Equal(1, welcome.Completed);
        Assert.Equal(1, welcome.Dismissed);
        Assert.Equal(month.AddDays(2), welcome.LastRunUtc);
        Assert.Equal(0.5m, welcome.CompletionRate);
        Assert.Equal(0.5m, welcome.DismissalRate);

        Assert.Equal(3, response.RecentEvents.Count);
        Assert.Equal(month.AddDays(7), response.RecentEvents.First().DateUtc);
        Assert.All(response.RecentEvents, productTourEvent => Assert.StartsWith("user-", productTourEvent.UserIdentity));
    }

    [Fact]
    public async Task GetProductTourUsageAsync_ForAllTime_ReturnsUsageAcrossMonths()
    {
        // Arrange
        var currentMonth = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        await CreateDataAsync(builder =>
        {
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog), currentMonth.AddMonths(-1), "user-1");
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.Automatic), currentMonth.AddDays(1), "user-2");
        });

        // Act
        var response = await SendRequestAsAsync<ProductTourUsageResponse>(request => request
            .AsGlobalAdminUser()
            .AppendPaths("admin", "product-tour-usage")
            .QueryString("all", "true")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(response);
        Assert.Null(response.Month);
        var overview = Assert.Single(response.Tours);
        Assert.Equal(2, overview.Started);
        Assert.Equal(1, overview.ManualStarted);
        Assert.Equal(0.5m, overview.ManualStartedRate);
    }

    [Fact]
    public Task GetProductTourUsageAsync_WithMonthAndAllTime_ReturnsValidationProblem()
    {
        // Act & Assert
        return SendRequestAsync(request => request
            .AsGlobalAdminUser()
            .AppendPaths("admin", "product-tour-usage")
            .QueryString("month", "2026-08-01")
            .QueryString("all", "true")
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public async Task GetProductTourUsageAsync_WithoutMonth_UsesCurrentUtcMonth()
    {
        // Arrange
        TimeProvider.SetUtcNow(new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc));

        // Act
        var response = await SendRequestAsAsync<ProductTourUsageResponse>(request => request
            .AsGlobalAdminUser()
            .AppendPaths("admin", "product-tour-usage")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(response);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), response.Month);
        Assert.Empty(response.Tours);
        Assert.Empty(response.RecentEvents);
    }

    [Fact]
    public Task GetProductTourUsageAsync_AsOrganizationUser_ReturnsForbidden()
    {
        // Act & Assert
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
