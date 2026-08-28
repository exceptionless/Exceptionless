using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Services;

namespace Exceptionless.Web.Assistant;

public sealed class AssistantModelSettingsService
{
    private readonly AppOptions _appOptions;
    private readonly SystemSettingsService _systemSettingsService;

    public AssistantModelSettingsService(
        SystemSettingsService systemSettingsService,
        AppOptions appOptions)
    {
        _systemSettingsService = systemSettingsService;
        _appOptions = appOptions;
    }

    public async Task<AssistantModelSettings> GetAsync() => CreateResponse(await _systemSettingsService.GetAsync());

    public async Task<AssistantModelSettings> SetModelAsync(string? model, string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        string? normalizedModel = model?.Trim();
        if (String.IsNullOrWhiteSpace(normalizedModel)
            || String.Equals(normalizedModel, _appOptions.AssistantOptions.Model, StringComparison.Ordinal))
            normalizedModel = null;

        var settings = await _systemSettingsService.UpdateAsync(userId, value => value.AssistantModel = normalizedModel);

        return CreateResponse(settings);
    }

    public async Task<AssistantModelSettings> SetEnabledAsync(bool? enabled, string userId)
    {
        bool? normalizedEnabled = enabled;
        if (normalizedEnabled == _appOptions.AssistantOptions.Enabled)
            normalizedEnabled = null;

        var settings = await _systemSettingsService.UpdateAsync(userId, value => value.AssistantEnabled = normalizedEnabled);
        return CreateResponse(settings);
    }

    private AssistantModelSettings CreateResponse(SystemSettings? settings)
    {
        string configuredModel = _appOptions.AssistantOptions.Model;
        string? modelOverride = settings?.AssistantModel;
        bool isModelOverridden = !String.IsNullOrWhiteSpace(modelOverride);
        bool configuredEnabled = _appOptions.AssistantOptions.Enabled;
        bool? enabledOverride = settings?.AssistantEnabled;

        return new AssistantModelSettings(
            isModelOverridden ? modelOverride! : configuredModel,
            configuredModel,
            isModelOverridden,
            enabledOverride ?? configuredEnabled,
            configuredEnabled,
            enabledOverride.HasValue,
            _appOptions.AssistantOptions.IsConfigured);
    }
}

public sealed record AssistantModelSettings(
    string Model,
    string ConfiguredModel,
    bool IsOverridden,
    bool Enabled,
    bool ConfiguredEnabled,
    bool IsEnabledOverridden,
    bool IsConfigured);
