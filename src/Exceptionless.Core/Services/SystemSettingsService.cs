using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Lock;
using Foundatio.Repositories;

namespace Exceptionless.Core.Services;

public sealed class SystemSettingsService
{
    private readonly AppOptions _appOptions;
    private readonly Func<Task<SystemSettings?>> _getSettingsAsync;
    private readonly ILockProvider? _lockProvider;
    private readonly Func<SystemSettings, Task> _saveSettingsAsync;
    private readonly TimeProvider _timeProvider;

    public SystemSettingsService(
        ISystemSettingsRepository repository,
        ILockProvider lockProvider,
        AppOptions appOptions,
        TimeProvider timeProvider)
        : this(
            () => repository.GetByIdAsync(SystemSettings.DefaultId, options => options.Cache()),
            async settings => await repository.SaveAsync(settings, options => options.Cache().ImmediateConsistency()),
            lockProvider,
            appOptions,
            timeProvider)
    {
    }

    internal SystemSettingsService(
        Func<Task<SystemSettings?>> getSettingsAsync,
        Func<SystemSettings, Task> saveSettingsAsync,
        AppOptions appOptions,
        TimeProvider timeProvider)
        : this(getSettingsAsync, saveSettingsAsync, null, appOptions, timeProvider)
    {
    }

    private SystemSettingsService(
        Func<Task<SystemSettings?>> getSettingsAsync,
        Func<SystemSettings, Task> saveSettingsAsync,
        ILockProvider? lockProvider,
        AppOptions appOptions,
        TimeProvider timeProvider)
    {
        _getSettingsAsync = getSettingsAsync;
        _saveSettingsAsync = saveSettingsAsync;
        _lockProvider = lockProvider;
        _appOptions = appOptions;
        _timeProvider = timeProvider;
    }

    public Task<SystemSettings?> GetAsync() => _getSettingsAsync();

    public async Task<SystemSettings> UpdateAsync(string userId, Action<SystemSettings> update)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(update);

        if (_lockProvider is null)
            return await UpdateCoreAsync(userId, update);

        await using var settingsLock = await _lockProvider.AcquireAsync("system-settings:update", TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
        return await UpdateCoreAsync(userId, update);
    }

    private async Task<SystemSettings> UpdateCoreAsync(string userId, Action<SystemSettings> update)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var settings = await _getSettingsAsync() ?? new SystemSettings
        {
            CreatedByUserId = userId,
            CreatedUtc = utcNow
        };

        update(settings);
        settings.UpdatedByUserId = userId;
        settings.UpdatedUtc = utcNow;
        await _saveSettingsAsync(settings);

        return settings;
    }

    public async Task<bool> IsAssistantEnabledAsync()
    {
        var settings = await _getSettingsAsync();
        return settings?.AssistantEnabled ?? _appOptions.AssistantOptions.Enabled;
    }

    public async Task<bool> IsEventSubmissionEnabledAsync()
    {
        var settings = await _getSettingsAsync();
        return settings?.EventSubmissionEnabled ?? !_appOptions.EventSubmissionDisabled;
    }
}
