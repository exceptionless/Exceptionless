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
    public StatusControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

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
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;

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
    public Task GetAboutAsync_Anonymous_ReturnsVersionInfo()
    {
        return SendRequestAsync(r => r
            .AppendPath("about")
            .StatusCodeShouldBeOk());
    }

    [Fact]
    public Task GetQueueStatsAsync_AsGlobalAdmin_ReturnsQueueStats()
    {
        return SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("queue-stats")
            .StatusCodeShouldBeOk());
    }

    [Fact]
    public Task GetQueueStatsAsync_AsNonAdmin_ReturnsForbidden()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPath("queue-stats")
            .StatusCodeShouldBeForbidden());
    }

    [Fact]
    public Task GetSystemNotificationAsync_WhenNoneSet_ReturnsOk()
    {
        return SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("notifications/system")
            .StatusCodeShouldBeOk());
    }

    [Fact]
    public async Task PostSystemNotificationAsync_WithMessage_ReturnsNotification()
    {
        // Arrange
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;

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
    public Task PostSystemNotificationAsync_WithEmptyMessage_ReturnsBadRequest()
    {
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("notifications/system")
            .Content(new ValueFromBody<string>(String.Empty))
            .StatusCodeShouldBeBadRequest());
    }

    [Fact]
    public Task PostSystemNotificationAsync_AsNonAdmin_ReturnsForbidden()
    {
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
        // Arrange
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

        // Assert
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
        return SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPath("notifications/system")
            .StatusCodeShouldBeForbidden());
    }

    [Fact]
    public Task GetNotificationSettings_AsGlobalAdmin_ReturnsSettings()
    {
        return SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("notifications/settings")
            .StatusCodeShouldBeOk());
    }

    [Fact]
    public Task GetNotificationSettings_AsNonAdmin_ReturnsForbidden()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPath("notifications/settings")
            .StatusCodeShouldBeForbidden());
    }

    [Fact]
    public async Task ForceRefresh_AsGlobalAdmin_ReturnsCriticalNotification()
    {
        // Arrange
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;

        // Act
        var notification = await SendRequestAsAsync<ReleaseNotification>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("notifications/force-refresh")
            .Content(new ValueFromBody<string?>("Deploy v9.0"))
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(notification);
        Assert.True(notification.Critical);
        Assert.Equal("Deploy v9.0", notification.Message);
        Assert.True(notification.Date.IsAfterOrEqual(utcNow));
    }

    [Fact]
    public async Task ForceRefresh_WithNoBody_ReturnsCriticalNotificationWithNullMessage()
    {
        // Arrange
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;

        // Act
        var notification = await SendRequestAsAsync<ReleaseNotification>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("notifications/force-refresh")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(notification);
        Assert.True(notification.Critical);
        Assert.Null(notification.Message);
        Assert.True(notification.Date.IsAfterOrEqual(utcNow));
    }

    [Fact]
    public Task ForceRefresh_AsNonAdmin_ReturnsForbidden()
    {
        return SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("notifications/force-refresh")
            .StatusCodeShouldBeForbidden());
    }

    [Fact]
    public async Task PostSystemNotificationAsync_WithPublishFalse_StoresWithoutPublishing()
    {
        // Arrange & Act
        var notification = await SendRequestAsAsync<SystemNotification>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("notifications/system")
            .QueryString("publish", "false")
            .Content(new ValueFromBody<string>("Silent notification"))
            .StatusCodeShouldBeOk());

        // Assert — notification is persisted and returned
        Assert.NotNull(notification);
        Assert.Equal("Silent notification", notification.Message);

        // Verify it was actually persisted to cache
        var persisted = await SendRequestAsAsync<SystemNotification>(r => r
            .AsGlobalAdminUser()
            .AppendPath("notifications/system")
            .StatusCodeShouldBeOk());
        Assert.NotNull(persisted);
        Assert.Equal("Silent notification", persisted.Message);
    }

    [Fact]
    public Task PostSystemNotificationAsync_WithMessageExceedingMaxLength_ReturnsBadRequest()
    {
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("notifications/system")
            .Content(new ValueFromBody<string>(new string('x', 1001)))
            .StatusCodeShouldBeBadRequest());
    }

    [Fact]
    public Task PostReleaseNotificationAsync_WithMessageExceedingMaxLength_ReturnsBadRequest()
    {
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("notifications/release")
            .Content(new ValueFromBody<string>(new string('x', 1001)))
            .StatusCodeShouldBeBadRequest());
    }

    [Fact]
    public Task ForceRefresh_WithMessageExceedingMaxLength_ReturnsBadRequest()
    {
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("notifications/force-refresh")
            .Content(new ValueFromBody<string?>(new string('x', 1001)))
            .StatusCodeShouldBeBadRequest());
    }
}
