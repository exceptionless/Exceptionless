using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
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
    public async Task GetProductTourUsageAsync_AsGlobalAdmin_ReturnsMonthlyCountsAndKnownRows()
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
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Shown, ProductTours.AppWelcome, 1, ProductTourLaunchSource.Welcome), month.AddDays(1), "user-1", 2);
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppWelcome, 1, ProductTourLaunchSource.Welcome), month.AddDays(1).AddHours(1), "user-1");
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Completed, ProductTours.AppWelcome, 1, ProductTourLaunchSource.Welcome), month.AddDays(1).AddHours(2), "user-1");
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Dismissed, ProductTours.AppWelcome, 1, ProductTourLaunchSource.Welcome), month.AddDays(2), "user-1");
            AddUsage(builder, "product-tour.started.unknown.v1.unknown-source", month.AddDays(8), "user-4");

            builder.Event()
                .TestProject()
                .Type(Event.KnownTypes.FeatureUsage)
                .Source("product-tour.started.ignored-tour.v1.catalog")
                .Date(month.AddDays(9))
                .UserIdentity("user-5");
            AddUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog), month.AddMonths(-1), "user-6");
        });

        // Act
        var response = await SendRequestAsAsync<ProductTourUsageResponse>(request => request
            .AsGlobalAdminUser()
            .AppendPaths("admin", "product-tour-usage")
            .QueryString("month", "2026-08-01")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(response);
        Assert.Equal(month, response.UtcStart);
        Assert.Equal(month.AddMonths(1), response.UtcEnd);
        Assert.Equal(ProductTourUsageInterval.Day, response.Interval);
        Assert.Equal(ProductTours.Definitions.Values.Sum(definition => definition.CurrentVersion), response.Tours.Count);

        var overview = Assert.Single(response.Tours, tour => String.Equals(tour.Name, ProductTours.AppOverview, StringComparison.Ordinal));
        Assert.Equal(1, overview.Version);
        Assert.Equal(ProductTourKind.Guide, overview.Kind);
        Assert.Equal(0, overview.Shown);
        Assert.Equal(3, overview.Started);
        Assert.Equal(1, overview.Completed);
        Assert.Equal(1, overview.Dismissed);
        Assert.Equal(month.AddDays(6), overview.LastRunUtc);
        Assert.Equal(2, Assert.Single(overview.StartSources, source => source.Source == ProductTourLaunchSource.Catalog).Count);
        Assert.Equal(1, Assert.Single(overview.StartSources, source => source.Source == ProductTourLaunchSource.HelpMenu).Count);

        var welcome = Assert.Single(response.Tours, tour => String.Equals(tour.Name, ProductTours.AppWelcome, StringComparison.Ordinal));
        Assert.Equal(ProductTourKind.Prompt, welcome.Kind);
        Assert.Equal(2, welcome.Shown);
        Assert.Equal(1, welcome.Started);
        Assert.Equal(1, welcome.Completed);
        Assert.Equal(1, welcome.Dismissed);
        Assert.Equal(1, Assert.Single(welcome.StartSources).Count);
        Assert.Equal(ProductTourLaunchSource.Welcome, welcome.StartSources.Single().Source);
        var welcomeDay = Assert.Single(welcome.Activity, period => period.DateUtc == month.AddDays(1));
        Assert.Equal(2, welcomeDay.Shown);
        Assert.Equal(1, welcomeDay.Started);
        Assert.Equal(1, welcomeDay.Completed);
        Assert.Equal(overview.Started, overview.Activity.Sum(period => period.Started));
        Assert.DoesNotContain(response.Tours, tour => tour.Started > 0 && (tour.Name is "unknown" or "ignored-tour"));
    }

    [Fact]
    public async Task GetProductTourUsageAsync_History_ReturnsConfiguredAvailableRange()
    {
        // Arrange
        var now = new DateTime(2026, 8, 21, 12, 30, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(now);
        var retainedEvent = now.SubtractDays(_appOptions.MaximumRetentionDays - 1);
        await CreateDataAsync(builder => AddUsage(builder,
            ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog),
            retainedEvent,
            "user-1"));

        // Act
        var response = await SendRequestAsAsync<ProductTourUsageResponse>(request => request
            .AsGlobalAdminUser()
            .AppendPaths("admin", "product-tour-usage")
            .QueryString("history", "true")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(response);
        Assert.Equal(now, response.UtcEnd);
        Assert.Equal(ProductTourUsageInterval.Auto, response.Interval);
        var overview = Assert.Single(response.Tours, tour => String.Equals(tour.Name, ProductTours.AppOverview, StringComparison.Ordinal));
        Assert.Equal(1, overview.Started);
        Assert.Equal(Assert.Single(overview.Activity, period => period.Started > 0).DateUtc, response.UtcStart);
        Assert.InRange(overview.Activity.Count, 80, 201);
    }

    [Fact]
    public Task GetProductTourUsageAsync_WithMonthAndHistory_ReturnsValidationProblem()
    {
        // Act & Assert
        return SendRequestAsync(request => request
            .AsGlobalAdminUser()
            .AppendPaths("admin", "product-tour-usage")
            .QueryString("month", "2026-08-01")
            .QueryString("history", "true")
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public async Task GetProductTourUsageAsync_RollingDaysAcrossFebruary_UsesDailyUtcBoundaries()
    {
        // Arrange
        var now = new DateTime(2026, 3, 2, 12, 30, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(now);
        var start = now.Date.AddDays(-29);
        var source = ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog);
        await CreateDataAsync(builder =>
        {
            AddUsage(builder, source, start.AddTicks(-1), "outside");
            AddUsage(builder, source, start, "boundary");
            AddUsage(builder, source, now.AddMinutes(-1), "today");
        });

        // Act
        var response = await SendRequestAsAsync<ProductTourUsageResponse>(request => request.AsGlobalAdminUser()
            .AppendPaths("admin", "product-tour-usage").QueryString("days", "30").StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(response);
        Assert.Equal(start, response.UtcStart);
        Assert.Equal(now, response.UtcEnd);
        Assert.Equal(ProductTourUsageInterval.Day, response.Interval);
        Assert.False(response.CollectionAvailable);
        Assert.Equal(2, Assert.Single(response.Tours, tour => String.Equals(tour.Name, ProductTours.AppOverview, StringComparison.Ordinal)).Started);
    }

    [Theory]
    [InlineData("0", "false")]
    [InlineData("91", "false")]
    [InlineData("30", "true")]
    public Task GetProductTourUsageAsync_InvalidRollingRange_ReturnsValidationProblem(string days, string history)
    {
        // Act & Assert
        return SendRequestAsync(request => request.AsGlobalAdminUser().AppendPaths("admin", "product-tour-usage")
            .QueryString("days", days).QueryString("history", history).StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public async Task GetProductTourUsageAsync_WithoutMonth_UsesCurrentUtcMonthAndReturnsZeroRows()
    {
        // Arrange
        var now = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(now);

        // Act
        var response = await SendRequestAsAsync<ProductTourUsageResponse>(request => request
            .AsGlobalAdminUser()
            .AppendPaths("admin", "product-tour-usage")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(response);
        Assert.Equal(now.StartOfMonth(), response.UtcStart);
        Assert.Equal(now.StartOfMonth().AddMonths(1), response.UtcEnd);
        Assert.Equal(ProductTours.Definitions.Values.Sum(definition => definition.CurrentVersion), response.Tours.Count);
        Assert.All(response.Tours, tour =>
        {
            Assert.Equal(0, tour.Shown);
            Assert.Equal(0, tour.Started);
            Assert.Equal(0, tour.Completed);
            Assert.Equal(0, tour.Dismissed);
            Assert.Empty(tour.StartSources);
            Assert.Empty(tour.Activity);
            Assert.Null(tour.LastRunUtc);
        });
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
