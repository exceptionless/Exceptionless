using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using Exceptionless.Web.Models.Admin;
using FluentRest;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public class AdminNotificationTests : IntegrationTestsBase
{
    public AdminNotificationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task GetNotificationSettings_AsGlobalAdmin_ReturnsSettings()
    {
        var settings = await SendRequestAsAsync<NotificationSettingsResponse>(r => r
            .AsGlobalAdminUser()
            .AppendPath("admin/notifications")
            .StatusCodeShouldBeOk());

        Assert.NotNull(settings);
    }

    [Fact]
    public Task GetNotificationSettings_AsNonAdmin_ReturnsForbidden()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPath("admin/notifications")
            .StatusCodeShouldBeForbidden());
    }

    [Fact]
    public async Task SetSystemNotification_AsGlobalAdmin_ReturnsNotification()
    {
        var notification = await SendRequestAsAsync<SystemNotification>(r => r
            .Put()
            .AsGlobalAdminUser()
            .AppendPath("admin/notifications/system")
            .Content(new SetSystemNotificationRequest { Message = "Maintenance tonight" })
            .StatusCodeShouldBeOk());

        Assert.NotNull(notification);
        Assert.Equal("Maintenance tonight", notification.Message);
    }

    [Fact]
    public Task SetSystemNotification_AsNonAdmin_ReturnsForbidden()
    {
        return SendRequestAsync(r => r
            .Put()
            .AsTestOrganizationUser()
            .AppendPath("admin/notifications/system")
            .Content(new SetSystemNotificationRequest { Message = "test" })
            .StatusCodeShouldBeForbidden());
    }

    [Fact]
    public Task SetSystemNotification_EmptyMessage_ReturnsValidationProblem()
    {
        return SendRequestAsync(r => r
            .Put()
            .AsGlobalAdminUser()
            .AppendPath("admin/notifications/system")
            .Content(new SetSystemNotificationRequest { Message = " " })
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public async Task ClearSystemNotification_AsGlobalAdmin_ReturnsOk()
    {
        // Arrange - set a notification first
        await SendRequestAsync(r => r
            .Put()
            .AsGlobalAdminUser()
            .AppendPath("admin/notifications/system")
            .Content(new SetSystemNotificationRequest { Message = "To be cleared" })
            .StatusCodeShouldBeOk());

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPath("admin/notifications/system")
            .StatusCodeShouldBeOk());

        // Assert - verify notification is gone
        var settings = await SendRequestAsAsync<NotificationSettingsResponse>(r => r
            .AsGlobalAdminUser()
            .AppendPath("admin/notifications")
            .StatusCodeShouldBeOk());

        Assert.Null(settings!.SystemNotification);
    }

    [Fact]
    public async Task SendReleaseNotification_NonCritical_ReturnsNotification()
    {
        var notification = await SendRequestAsAsync<ReleaseNotification>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("admin/notifications/release")
            .Content(new SendReleaseNotificationRequest { Message = "v8.0 released", Critical = false })
            .StatusCodeShouldBeOk());

        Assert.NotNull(notification);
        Assert.Equal("v8.0 released", notification.Message);
        Assert.False(notification.Critical);
    }

    [Fact]
    public async Task SendReleaseNotification_Critical_ReturnsNotification()
    {
        var notification = await SendRequestAsAsync<ReleaseNotification>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("admin/notifications/release")
            .Content(new SendReleaseNotificationRequest { Message = "Breaking change", Critical = true })
            .StatusCodeShouldBeOk());

        Assert.NotNull(notification);
        Assert.Equal("Breaking change", notification.Message);
        Assert.True(notification.Critical);
    }

    [Fact]
    public async Task ForceRefresh_AsGlobalAdmin_ReturnsCriticalNotification()
    {
        var notification = await SendRequestAsAsync<ReleaseNotification>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("admin/notifications/force-refresh")
            .Content(new ForceRefreshRequest { Message = "Deploy in progress" })
            .StatusCodeShouldBeOk());

        Assert.NotNull(notification);
        Assert.True(notification.Critical);
        Assert.Equal("Deploy in progress", notification.Message);
    }

    [Fact]
    public async Task ForceRefresh_WithNoBody_UsesDefaultMessage()
    {
        var notification = await SendRequestAsAsync<ReleaseNotification>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("admin/notifications/force-refresh")
            .StatusCodeShouldBeOk());

        Assert.NotNull(notification);
        Assert.True(notification.Critical);
    }

    [Fact]
    public Task ForceRefresh_AsNonAdmin_ReturnsForbidden()
    {
        return SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("admin/notifications/force-refresh")
            .Content(new ForceRefreshRequest { Message = "test" })
            .StatusCodeShouldBeForbidden());
    }

    // Verify legacy StatusController endpoints still work
    [Fact]
    public Task LegacyGetSystemNotification_StillWorks()
    {
        return SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("notifications/system")
            .StatusCodeShouldBeOk());
    }

    [Fact]
    public async Task LegacyPostSystemNotification_StillWorks()
    {
        var notification = await SendRequestAsAsync<SystemNotification>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("notifications/system")
            .Content(new ValueFromBody<string>("Legacy test"))
            .StatusCodeShouldBeOk());

        Assert.NotNull(notification);
        Assert.Equal("Legacy test", notification.Message);
    }

    [Fact]
    public Task LegacyDeleteSystemNotification_StillWorks()
    {
        return SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPath("notifications/system")
            .StatusCodeShouldBeOk());
    }
}
