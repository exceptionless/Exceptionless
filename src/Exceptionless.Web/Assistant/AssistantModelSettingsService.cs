using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;

namespace Exceptionless.Web.Assistant;

public sealed class AssistantModelSettingsService
{
    private readonly AppOptions _appOptions;
    private readonly Func<Task<SystemSettings?>> _getSettingsAsync;
    private readonly Func<SystemSettings, Task> _saveSettingsAsync;
    private readonly TimeProvider _timeProvider;

    public AssistantModelSettingsService(
        ISystemSettingsRepository repository,
        AppOptions appOptions,
        TimeProvider timeProvider)
        : this(
            () => repository.GetByIdAsync(SystemSettings.DefaultId, options => options.Cache()),
            async settings => await repository.SaveAsync(settings, options => options.Cache().ImmediateConsistency()),
            appOptions,
            timeProvider)
    {
    }

    internal AssistantModelSettingsService(
        Func<Task<SystemSettings?>> getSettingsAsync,
        Func<SystemSettings, Task> saveSettingsAsync,
        AppOptions appOptions,
        TimeProvider timeProvider)
    {
        _getSettingsAsync = getSettingsAsync;
        _saveSettingsAsync = saveSettingsAsync;
        _appOptions = appOptions;
        _timeProvider = timeProvider;
    }

    public async Task<AssistantModelSettings> GetAsync() => CreateResponse(await _getSettingsAsync());

    public async Task<AssistantModelSettings> SetModelAsync(string? model, string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        string? normalizedModel = model?.Trim();
        if (String.IsNullOrWhiteSpace(normalizedModel)
            || String.Equals(normalizedModel, _appOptions.AssistantOptions.Model, StringComparison.Ordinal))
            normalizedModel = null;

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var settings = await _getSettingsAsync() ?? new SystemSettings
        {
            CreatedByUserId = userId,
            CreatedUtc = utcNow
        };
        settings.AssistantModel = normalizedModel;
        settings.UpdatedByUserId = userId;
        settings.UpdatedUtc = utcNow;

        await _saveSettingsAsync(settings);

        return CreateResponse(settings);
    }

    private AssistantModelSettings CreateResponse(SystemSettings? settings)
    {
        string configuredModel = _appOptions.AssistantOptions.Model;
        string? modelOverride = settings?.AssistantModel;
        bool isOverridden = !String.IsNullOrWhiteSpace(modelOverride);

        return new AssistantModelSettings(
            isOverridden ? modelOverride! : configuredModel,
            configuredModel,
            isOverridden);
    }
}

public sealed record AssistantModelSettings(string Model, string ConfiguredModel, bool IsOverridden);
