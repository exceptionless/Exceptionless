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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SetFullLoggingEnabledAsync_NewAndLegacySettings_DefaultOffAndPreserveOtherSettings(bool hasLegacyRecord)
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

        Assert.False((await service.GetAsync()).FullLoggingEnabled);
        Assert.False(await systemSettings.IsAssistantFullLoggingEnabledAsync());

        Assert.True((await service.SetFullLoggingEnabledAsync(true, "admin-user")).FullLoggingEnabled);
        Assert.True(await systemSettings.IsAssistantFullLoggingEnabledAsync());
        await service.SetModelAsync("example/model", "admin-user");
        await service.SetEnabledAsync(true, "admin-user");
        Assert.True((await service.GetAsync()).FullLoggingEnabled);

        Assert.False((await service.SetFullLoggingEnabledAsync(false, "admin-user")).FullLoggingEnabled);
        Assert.False(await systemSettings.IsAssistantFullLoggingEnabledAsync());
        Assert.NotNull(persisted);
        Assert.Equal("example/model", persisted.AssistantModel);
        Assert.True(persisted.AssistantEnabled);
        Assert.Equal("admin-user", persisted.UpdatedByUserId);
    }
}
