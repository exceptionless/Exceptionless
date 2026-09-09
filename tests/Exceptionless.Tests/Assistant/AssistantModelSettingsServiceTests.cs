using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Services;
using Exceptionless.Web.Assistant;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Exceptionless.Tests.Assistant;

public sealed class AssistantModelSettingsServiceTests
{
    [Fact]
    public void LegacyUser_FollowsConversationSharingDefault()
    {
        var user = JsonSerializer.Deserialize<User>("{}");
        Assert.NotNull(user);
        Assert.Null(user.AssistantConversationSharingEnabled);
        Assert.False(AssistantConversationSharingService.Resolve(user.AssistantConversationSharingEnabled, false).Enabled);
        Assert.True(AssistantConversationSharingService.Resolve(user.AssistantConversationSharingEnabled, true).Enabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SetConversationSharingDefaultEnabledAsync_NewAndLegacySettings_DefaultOffAndPreserveOtherSettings(bool hasLegacyRecord)
    {
        var options = AppOptions.ReadFromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BaseURL"] = "https://localhost" }).Build());
        SystemSettings? persisted = hasLegacyRecord ? JsonSerializer.Deserialize<SystemSettings>("{}") : null;
        var systemSettings = new SystemSettingsService(() => Task.FromResult(persisted), value =>
        {
            persisted = value;
            return Task.CompletedTask;
        }, options, TimeProvider.System);
        var service = new AssistantModelSettingsService(systemSettings, options);

        Assert.False((await service.GetAsync()).ConversationSharingDefaultEnabled);
        Assert.False(await systemSettings.IsAssistantConversationSharingDefaultEnabledAsync());

        Assert.True((await service.SetConversationSharingDefaultEnabledAsync(true, "admin-user")).ConversationSharingDefaultEnabled);
        Assert.True(await systemSettings.IsAssistantConversationSharingDefaultEnabledAsync());
        await service.SetModelAsync("example/model", "admin-user");
        await service.SetEnabledAsync(true, "admin-user");
        Assert.True((await service.GetAsync()).ConversationSharingDefaultEnabled);

        Assert.False((await service.SetConversationSharingDefaultEnabledAsync(false, "admin-user")).ConversationSharingDefaultEnabled);
        Assert.False(await systemSettings.IsAssistantConversationSharingDefaultEnabledAsync());
        Assert.NotNull(persisted);
        Assert.Equal("example/model", persisted.AssistantModel);
        Assert.True(persisted.AssistantEnabled);
        Assert.Equal("admin-user", persisted.UpdatedByUserId);
    }
}
