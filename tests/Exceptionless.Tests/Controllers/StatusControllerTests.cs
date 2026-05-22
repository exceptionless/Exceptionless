using System.Net;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using FluentRest;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public class StatusControllerTests : IntegrationTestsBase
{
    public StatusControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();

        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    //[InlineData(null, true, false)] // TODO: Resolve issue where you are required to pass a message via the body.
    [InlineData("New Release!!", false)]
    [InlineData("New Release!!", true)]
    public async Task CanSendReleaseNotification(string? message, bool critical, bool sendMessageAsContentIfEmpty = true)
    {
        var utcNow = DateTime.UtcNow;

        ReleaseNotification? notification;
        if (!String.IsNullOrEmpty(message) || sendMessageAsContentIfEmpty)
        {
            notification = await SendRequestAsAsync<ReleaseNotification>(r => r
                .Post()
                .AsGlobalAdminUser()
                .AppendPath("notifications/release")
                .QueryStringIf(() => critical, "critical", critical)
                .Content(new ValueFromBody<string?>(message))
                .StatusCodeShouldBeOk());
        }
        else
        {
            notification = await SendRequestAsAsync<ReleaseNotification>(r => r
                .Post()
                .AsGlobalAdminUser()
                .AppendPath("notifications/release")
                .QueryStringIf(() => critical, "critical", critical)
                .StatusCodeShouldBeOk());
        }

        Assert.NotNull(notification);
        Assert.Equal(message, notification.Message);
        Assert.Equal(critical, notification.Critical);
        Assert.True(notification.Date.IsAfterOrEqual(utcNow));
    }

    [Fact]
    public async Task GetAboutAsync_Anonymous_ReturnsVersionInfo()
    {
        // Arrange & Act
        var response = await SendRequestAsync(r => r
            .AppendPath("about")
            .StatusCodeShouldBeOk());

        // Assert
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("informationalVersion", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("appMode", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("machineName", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetQueueStatsAsync_AsGlobalAdmin_ReturnsQueueStats()
    {
        // Arrange & Act
        var response = await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("queue-stats")
            .StatusCodeShouldBeOk());

        // Assert
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("eventPosts", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mailMessages", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("notifications", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("webHooks", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public Task GetQueueStatsAsync_AsNonAdmin_ReturnsForbidden()
    {
        // Arrange & Act
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPath("queue-stats")
            .StatusCodeShouldBeForbidden());
    }

    [Fact]
    public async Task GetSystemNotificationAsync_WhenNoneSet_ReturnsOk()
    {
        // Arrange & Act
        var response = await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("notifications/system")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostSystemNotificationAsync_WithMessage_ReturnsNotification()
    {
        // Arrange
        var utcNow = DateTime.UtcNow;

        // Act
        var notification = await SendRequestAsAsync<SystemNotification>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("notifications/system")
            .Content(new ValueFromBody<string>("System maintenance scheduled"))
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(notification);
        Assert.Equal("System maintenance scheduled", notification.Message);
        Assert.True(notification.Date.IsAfterOrEqual(utcNow));
    }

    [Fact]
    public Task PostSystemNotificationAsync_WithEmptyMessage_ReturnsNotFound()
    {
        // Arrange & Act
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("notifications/system")
            .Content(new ValueFromBody<string>(""))
            .StatusCodeShouldBeNotFound());
    }

    [Fact]
    public Task PostSystemNotificationAsync_AsNonAdmin_ReturnsForbidden()
    {
        // Arrange & Act
        return SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("notifications/system")
            .Content(new ValueFromBody<string>("test"))
            .StatusCodeShouldBeForbidden());
    }

    [Fact]
    public async Task RemoveSystemNotificationAsync_AsGlobalAdmin_ReturnsOk()
    {
        // Arrange - first set a notification
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("notifications/system")
            .Content(new ValueFromBody<string>("To be removed"))
            .StatusCodeShouldBeOk());

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPath("notifications/system")
            .StatusCodeShouldBeOk());

        // Assert - get should now return empty
        var response = await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("notifications/system")
            .StatusCodeShouldBeOk());

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("To be removed", content);
    }

    [Fact]
    public Task RemoveSystemNotificationAsync_AsNonAdmin_ReturnsForbidden()
    {
        // Arrange & Act
        return SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPath("notifications/system")
            .StatusCodeShouldBeForbidden());
    }
}
