using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Web.Assistant;

public sealed class AssistantConversationSharingService(IUserRepository userRepository, SystemSettingsService systemSettingsService)
{
    public async Task<AssistantConversationSharingSettings?> GetAsync(string userId)
    {
        // Read the saved choice for every turn, including choices made on another device.
        var user = await userRepository.GetByIdAsync(userId);
        if (user is null)
            return null;

        bool defaultEnabled = await systemSettingsService.IsAssistantConversationSharingDefaultEnabledAsync();
        return Resolve(user.AssistantConversationSharingEnabled, defaultEnabled);
    }

    public async Task<AssistantConversationSharingSettings?> SetAsync(string userId, bool? enabled)
    {
        // Preserve an explicit choice even when it matches today's default.
        bool updated = await userRepository.PatchAsync(userId,
            new ActionPatch<User>(user => user.AssistantConversationSharingEnabled = enabled),
            options => options.Cache().ImmediateConsistency());
        return updated ? await GetAsync(userId) : null;
    }

    internal static AssistantConversationSharingSettings Resolve(bool? enabled, bool defaultEnabled) =>
        new(enabled ?? defaultEnabled, defaultEnabled, enabled.HasValue);
}

public sealed record AssistantConversationSharingSettings(bool Enabled, bool DefaultEnabled, bool IsOverridden);

public sealed record UpdateAssistantConversationSharing
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
    public required bool? Enabled { get; init; }
}
