using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class ProductTourActivityEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : IntegrationTestsBase(output, factory)
{
    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<IQueue<EventPost>>().DeleteQueueAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task RecordProductTourActivity_ValidRequest_QueuesOnlyAllowlistedData()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        TimeProvider.SetUtcNow(now);
        await GetService<IProjectRepository>().AddAsync(new Project
        {
            Id = GetService<AppOptions>().InternalProjectId,
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Guide activity",
            NextSummaryEndOfDayTicks = now.UtcDateTime.Date.AddDays(1).AddHours(1).Ticks
        });

        // Act
        await SendRequestAsync(request => request.Post().AsTestOrganizationUser()
            .AppendPath("users/me/product-tours/app-overview/activity")
            .Content(new PostProductTourActivity { Version = 1, Action = ProductTourTelemetryEvent.StepReached, Source = ProductTourLaunchSource.Catalog, Step = "navigation" })
            .StatusCodeShouldBeAccepted());

        // Assert
        var entry = await GetService<IQueue<EventPost>>().DequeueAsync(TimeSpan.FromSeconds(1));
        Assert.NotNull(entry);
        Assert.Null(entry.Value.IpAddress);
        Assert.Null(entry.Value.UserAgent);
        Assert.Null(entry.Value.ClientKeyHash);
        Assert.Equal(GetService<AppOptions>().InternalProjectId, entry.Value.ProjectId);
        var payload = await GetService<EventPostService>().GetEventPostPayloadAsync(Path.ChangeExtension(entry.Value.FilePath, ".payload"));
        Assert.NotNull(payload);
        using var document = JsonDocument.Parse(payload);
        var ev = document.RootElement;
        Assert.Equal("product-tour.step-reached.app-overview.v1.catalog", ev.GetProperty("source").GetString());
        Assert.Equal(now, ev.GetProperty("date").GetDateTimeOffset());
        Assert.Equal("product-tour-step:navigation", Assert.Single(ev.GetProperty("tags").EnumerateArray()).GetString());
        Assert.DoesNotContain(ev.EnumerateObject(), property => property.Name.StartsWith('@'));
        if (ev.TryGetProperty("data", out var data))
        {
            Assert.Empty(data.EnumerateObject());
        }
        await entry.CompleteAsync();
    }

    [Fact]
    public async Task UpdateCurrentUserProductTourAnalytics_ConcurrentProgress_PreservesBothAndStopsCollection()
    {
        // Arrange
        var user = await GetService<IUserRepository>().GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);

        // Act
        await Task.WhenAll(
            SendRequestAsync(request => request.Put().AsTestOrganizationUser().AppendPath("users/me/product-tour-analytics")
                .Content(new UpdateProductTourAnalytics { Enabled = false }).StatusCodeShouldBeNoContent()),
            SendRequestAsync(request => request.Put().AsTestOrganizationUser().AppendPath("users/me/product-tours/app-overview")
                .Content(new UpdateProductTourProgress { Status = ProductTourStatus.Completed, Version = 1 }).StatusCodeShouldBeOk()));
        await SendRequestAsync(request => request.Post().AsTestOrganizationUser().AppendPath("users/me/product-tours/app-overview/activity")
            .Content(new PostProductTourActivity { Version = 1, Action = ProductTourTelemetryEvent.Completed, Source = ProductTourLaunchSource.Catalog })
            .StatusCodeShouldBeNoContent());

        // Assert
        var persisted = await GetService<IUserRepository>().GetByIdAsync(user.Id, options => options.Cache(false));
        Assert.NotNull(persisted);
        Assert.False(persisted.ProductTourAnalyticsEnabled);
        Assert.Equal(ProductTourStatus.Completed, persisted.ProductTours[ProductTours.AppOverview].Status);
        Assert.Equal(0, (await GetService<IQueue<EventPost>>().GetQueueStatsAsync()).Enqueued);
    }

    [Fact]
    public void DeserializeUser_LegacyRecord_DefaultsAnalyticsToEnabled()
    {
        // Arrange & Act
        var user = GetService<ITextSerializer>().Deserialize<User>("{}");

        // Assert
        Assert.NotNull(user);
        Assert.True(user.ProductTourAnalyticsEnabled);
        Assert.Empty(user.ProductTours);
    }

    [Theory]
    [InlineData("unknown", 1, "navigation")]
    [InlineData("app-overview", 2, "navigation")]
    [InlineData("app-overview", 1, "password")]
    [InlineData("app-overview", 1, null)]
    public Task RecordProductTourActivity_InvalidCatalogValue_RejectsRequest(string name, int version, string? step)
    {
        // Act & Assert
        return SendRequestAsync(request => request.Post().AsTestOrganizationUser().AppendPath($"users/me/product-tours/{name}/activity")
            .Content(new PostProductTourActivity { Version = version, Action = ProductTourTelemetryEvent.StepReached, Source = ProductTourLaunchSource.Catalog, Step = step })
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public Task RecordProductTourActivity_ArbitraryContext_RejectsRequest()
    {
        // Act & Assert
        return SendRequestAsync(request => request.Post().AsTestOrganizationUser().AppendPath("users/me/product-tours/app-overview/activity")
            .Content("""{"version":1,"action":"started","source":"catalog","email":"private@example.test"}""", "application/json")
            .StatusCodeShouldBeBadRequest());
    }

    [Theory]
    [InlineData("{\"version\":1,\"action\":999,\"source\":\"catalog\"}")]
    [InlineData("{\"version\":1,\"action\":\"started\",\"source\":999}")]
    public Task RecordProductTourActivity_InvalidEnum_RejectsRequest(string payload)
    {
        // Act & Assert
        return SendRequestAsync(request => request.Post().AsTestOrganizationUser().AppendPath("users/me/product-tours/app-overview/activity")
            .Content(payload, "application/json").StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public Task UpdateCurrentUserProductTourAnalytics_ClientToken_IsForbidden()
    {
        // Act & Assert
        return SendRequestAsync(request => request.Put().AsTestOrganizationClientUser().AppendPath("users/me/product-tour-analytics")
            .Content(new UpdateProductTourAnalytics { Enabled = false }).StatusCodeShouldBeForbidden());
    }

    [Fact]
    public async Task RecordProductTourActivity_ProcessedEvent_HasNoIdentifyingContext()
    {
        // Arrange
        await GetService<IProjectRepository>().AddAsync(new Project
        {
            Id = GetService<AppOptions>().InternalProjectId,
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Guide activity",
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Date.AddDays(1).AddHours(1).Ticks
        });

        // Act
        await SendRequestAsync(request => request.Post().AsTestOrganizationUser().AppendPath("users/me/product-tours/app-overview/activity")
            .Content(new PostProductTourActivity { Version = 1, Action = ProductTourTelemetryEvent.Dismissed, Source = ProductTourLaunchSource.Catalog, Step = "navigation" })
            .StatusCodeShouldBeAccepted());
        var result = await GetService<EventPostsJob>().RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert
        Assert.True(result.IsSuccess);
        var ev = Assert.Single((await GetService<IEventRepository>().GetAllAsync()).Documents);
        Assert.Equal(GetService<AppOptions>().InternalProjectId, ev.ProjectId);
        Assert.Equal("product-tour.dismissed.app-overview.v1.catalog", ev.Source);
        Assert.Contains("product-tour-step:navigation", ev.Tags!);
        Assert.Empty(ev.Data!);
    }

    [Fact]
    public Task RecordProductTourActivity_ClientToken_IsForbidden()
    {
        // Act & Assert
        return SendRequestAsync(request => request.Post().AsTestOrganizationClientUser().AppendPath("users/me/product-tours/app-overview/activity")
            .Content(new PostProductTourActivity { Version = 1, Action = ProductTourTelemetryEvent.Started, Source = ProductTourLaunchSource.Catalog })
            .StatusCodeShouldBeForbidden());
    }

    [Fact]
    public async Task RecordProductTourActivity_MissingInternalProject_ReturnsUnavailable()
    {
        // Act & Assert
        var response = await SendRequestAsync(request => request.Post().AsTestOrganizationUser().AppendPath("users/me/product-tours/app-overview/activity")
            .Content(new PostProductTourActivity { Version = 1, Action = ProductTourTelemetryEvent.Started, Source = ProductTourLaunchSource.Catalog })
            .ExpectedStatus(System.Net.HttpStatusCode.ServiceUnavailable));
        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
