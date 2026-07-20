using System.Collections.Concurrent;
using System.Text.Json;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services.SourceMaps;

public sealed class SourceMapService : IDisposable
{
    public const string HttpClientName = "SourceMaps";
    internal const string GeneratedFileHttpClientName = "SourceMapGeneratedFiles";
    private static readonly TimeSpan FailureCacheLifetime = TimeSpan.FromMinutes(15);
    private const int MaximumLocalUsageEntries = 100_000;
    private readonly SemaphoreSlim _downloadSemaphore;
    private readonly ConcurrentDictionary<string, Lazy<Task<ResolvedSourceMap?>>> _inflightSourceMaps = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ResolvedSourceMap> _parsedSourceMapEntries = new(StringComparer.Ordinal);
    private readonly MemoryCache _parsedSourceMaps;
    private readonly MemoryCache _recentlyTrackedUsages;
    private readonly SourceMapDownloader _downloader;
    private readonly SourceMapStorage _storage;
    private readonly SourceMapRequestThrottle _throttle;
    private readonly ICacheClient _cache;
    private readonly ILockProvider _lockProvider;
    private readonly SourceMapOptions _options;
    private readonly TimeSpan _usageCacheLifetime;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SourceMapService> _logger;

    public SourceMapService(
        IHttpClientFactory httpClientFactory,
        IFileStorage storage,
        ICacheClient cache,
        ILockProvider lockProvider,
        SourceMapRequestThrottle throttle,
        JsonSerializerOptions serializerOptions,
        AppOptions options,
        TimeProvider timeProvider,
        ILogger<SourceMapService> logger)
    {
        _downloader = new SourceMapDownloader(httpClientFactory, options, throttle);
        _storage = new SourceMapStorage(storage, serializerOptions, logger);
        _throttle = throttle;
        _cache = cache;
        _lockProvider = lockProvider;
        _options = options.SourceMapOptions;
        _usageCacheLifetime = TimeSpan.FromDays(Math.Max(_options.FreeArtifactRetentionDays, _options.ArtifactRetentionDays) + 1L);
        _downloadSemaphore = new SemaphoreSlim(_options.MaximumConcurrentDownloads);
        _parsedSourceMaps = new MemoryCache(new MemoryCacheOptions { SizeLimit = _options.MaximumParsedSourceMapCacheSize });
        _recentlyTrackedUsages = new MemoryCache(new MemoryCacheOptions { SizeLimit = MaximumLocalUsageEntries });
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task<SourceMapArtifact> SaveUploadedAsync(
        string projectId,
        string generatedFileUrl,
        string? fileName,
        Stream stream,
        CancellationToken cancellationToken = default)
        => SaveUploadedAsync(projectId, generatedFileUrl, fileName, stream, false, cancellationToken);

    public async Task<SourceMapArtifact> SaveUploadedAsync(
        string projectId,
        string generatedFileUrl,
        string? fileName,
        Stream stream,
        bool isFreePlan,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeGeneratedFileUrl(generatedFileUrl, requireHttps: false, out var generatedFileUri))
            throw new ArgumentException("The generated file URL must be an absolute HTTP or HTTPS URL without credentials or a fragment.", nameof(generatedFileUrl));

        byte[] sourceMap = await SourceMapContent.ReadLimitedAsync(stream, _options.MaximumSourceMapSize, cancellationToken);
        _ = SourceMapDocument.Parse(sourceMap, _options.MaximumMappingSegments);

        string normalizedUrl = generatedFileUri.AbsoluteUri;
        var artifact = new SourceMapArtifact
        {
            Id = GetArtifactId(normalizedUrl),
            GeneratedFileUrl = normalizedUrl,
            FileName = String.IsNullOrWhiteSpace(fileName) ? null : Path.GetFileName(fileName),
            Size = sourceMap.LongLength,
            IsAutoDownloaded = false,
            CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime
        };

        string cacheKey = GetMemoryCacheKey(projectId, normalizedUrl);
        await WaitForInflightSourceMapAsync(cacheKey, cancellationToken);
        await using var artifactLock = await _lockProvider.TryAcquireAsync(GetArtifactLockKey(projectId, artifact.Id), TimeSpan.FromSeconds(30), cancellationToken);
        if (artifactLock is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new IOException("Unable to acquire the source map storage lock.");
        }

        await using var projectLock = await TryAcquireProjectStorageLockAsync(projectId, cancellationToken);
        if (projectLock is null)
            throw new IOException("Unable to acquire the project source map storage lock.");

        await ValidateStorageLimitAsync(projectId, artifact, isFreePlan, cancellationToken);
        await _storage.SaveAsync(projectId, artifact, sourceMap, cancellationToken);
        await ClearCachesAsync(projectId, normalizedUrl);
        return artifact;
    }

    public Task<IReadOnlyCollection<SourceMapArtifact>> GetArtifactsAsync(string projectId, CancellationToken cancellationToken = default)
        => _storage.GetArtifactsAsync(projectId, cancellationToken);

    public async Task<bool> DeleteArtifactAsync(string projectId, string artifactId, CancellationToken cancellationToken = default)
    {
        if (!IsArtifactId(artifactId))
            return false;

        await WaitForInflightArtifactAsync(projectId, artifactId, cancellationToken);
        await using var artifactLock = await _lockProvider.TryAcquireAsync(GetArtifactLockKey(projectId, artifactId), TimeSpan.FromSeconds(30), cancellationToken);
        if (artifactLock is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }

        await using var projectLock = await TryAcquireProjectStorageLockAsync(projectId, cancellationToken);
        if (projectLock is null)
            return false;

        var artifact = await _storage.DeleteAsync(projectId, artifactId, cancellationToken);
        if (artifact is null)
            return false;

        await RemoveUsageTrackingAsync(projectId, artifactId);
        await ClearCachesAsync(projectId, artifact.GeneratedFileUrl);
        return true;
    }

    public async Task DeleteAllArtifactsAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await using var projectLock = await TryAcquireProjectStorageLockAsync(projectId, cancellationToken);
        if (projectLock is null)
            throw new IOException("Unable to acquire the project source map storage lock.");

        var artifacts = await _storage.GetArtifactsAsync(projectId, cancellationToken);
        await _storage.DeleteAllAsync(projectId, cancellationToken);
        foreach (var artifact in artifacts)
            await RemoveUsageTrackingAsync(projectId, artifact.Id);
        await AdvanceProjectCacheVersionAsync(projectId);
        foreach (string key in _parsedSourceMapEntries.Keys.Where(key => key.StartsWith(projectId + ':', StringComparison.Ordinal)))
            _parsedSourceMaps.Remove(key);
        foreach (string key in _inflightSourceMaps.Keys.Where(key => key.StartsWith(projectId + ':', StringComparison.Ordinal)))
            _inflightSourceMaps.TryRemove(key, out _);
    }

    public Task<bool> SymbolicateAsync(string projectId, InnerError? error, CancellationToken cancellationToken = default)
        => SymbolicateAsync(new SourceMapRequest(projectId, projectId, null, false), error, cancellationToken);

    internal async Task<bool> SymbolicateAsync(SourceMapRequest request, InnerError? error, CancellationToken cancellationToken = default)
    {
        bool changed = false;
        int framesProcessed = 0;
        using var processingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        processingCancellationTokenSource.CancelAfter(_options.MaximumProcessingTime);
        try
        {
            while (error is not null)
            {
                if (error.StackTrace is not null)
                {
                    foreach (var frame in error.StackTrace)
                    {
                        if (++framesProcessed > _options.MaximumFramesPerError)
                            return changed;

                        if (await SymbolicateFrameAsync(request, frame, processingCancellationTokenSource.Token))
                            changed = true;
                    }
                }

                error = error.Inner;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Source map processing exceeded its time budget for project {ProjectId}.", request.ProjectId);
        }

        return changed;
    }

    public void Dispose()
    {
        _parsedSourceMaps.Dispose();
        _recentlyTrackedUsages.Dispose();
        _downloadSemaphore.Dispose();
    }

    private async Task<bool> SymbolicateFrameAsync(SourceMapRequest request, StackFrame frame, CancellationToken cancellationToken)
    {
        if (frame.Data?.ContainsKey(StackFrame.KnownDataKeys.SourceMap) == true || frame.LineNumber is null || frame.LineNumber < 1 || frame.Column is null || frame.Column < 1 || String.IsNullOrWhiteSpace(frame.FileName))
            return false;

        if (!TryNormalizeGeneratedFileUrl(frame.FileName, requireHttps: false, out var generatedFileUri))
            return false;

        var resolved = await GetSourceMapAsync(request, generatedFileUri, cancellationToken);
        if (resolved is null)
            return false;

        int generatedColumn = frame.Column.Value - 1;

        var original = resolved.Document.FindOriginalLocation(frame.LineNumber.Value - 1, generatedColumn);
        if (original is null)
            return false;

        frame.Data ??= new DataDictionary();
        frame.Data[StackFrame.KnownDataKeys.SourceMap] = new DataDictionary
        {
            ["generated_file_name"] = frame.FileName,
            ["generated_line_number"] = frame.LineNumber,
            ["generated_column"] = frame.Column,
            ["generated_name"] = frame.Name,
            ["source_map_id"] = resolved.Artifact.Id
        };
        frame.FileName = original.Source;
        frame.LineNumber = original.Line + 1;
        frame.Column = original.Column + 1;
        frame.Name = String.IsNullOrWhiteSpace(original.Name) ? null : original.Name;

        await TrackUsageAsync(request.ProjectId, resolved.Artifact.Id);

        return true;
    }

    private async Task<ResolvedSourceMap?> GetSourceMapAsync(SourceMapRequest request, Uri generatedFileUri, CancellationToken cancellationToken)
    {
        string cacheKey = GetMemoryCacheKey(request.ProjectId, generatedFileUri.AbsoluteUri);
        if (_parsedSourceMaps.TryGetValue(cacheKey, out ResolvedSourceMap? cached) && cached is not null)
        {
            long cacheVersion = await GetProjectCacheVersionAsync(request.ProjectId);
            if (cached.CacheVersion == cacheVersion && !ShouldRefresh(cached.Artifact, generatedFileUri))
                return cached;
            _parsedSourceMaps.Remove(cacheKey);
        }

        var lazy = _inflightSourceMaps.GetOrAdd(cacheKey, _ => new Lazy<Task<ResolvedSourceMap?>>(
            () => LoadAndCacheSourceMapAsync(request, generatedFileUri, cacheKey),
            LazyThreadSafetyMode.ExecutionAndPublication));
        Task<ResolvedSourceMap?> loadTask = lazy.Value;

        try
        {
            return await loadTask.WaitAsync(cancellationToken);
        }
        finally
        {
            if (loadTask.IsCompleted)
                RemoveInflightSourceMap(cacheKey, lazy);
            else
                _ = RemoveInflightSourceMapWhenCompleteAsync(cacheKey, lazy, loadTask);
        }
    }

    private async Task<ResolvedSourceMap?> LoadAndCacheSourceMapAsync(SourceMapRequest request, Uri generatedFileUri, string cacheKey)
    {
        long cacheVersion = await GetProjectCacheVersionAsync(request.ProjectId);
        var resolved = await LoadSourceMapAsync(request, generatedFileUri, cacheVersion);
        long currentCacheVersion = await GetProjectCacheVersionAsync(request.ProjectId);
        if ((resolved is null && cacheVersion != currentCacheVersion)
            || (resolved is not null && resolved.CacheVersion != currentCacheVersion))
        {
            resolved = await LoadSourceMapAsync(request, generatedFileUri, currentCacheVersion);
        }

        if (resolved is not null && resolved.Document.EstimatedMemorySize <= _options.MaximumParsedSourceMapCacheSize)
        {
            _parsedSourceMapEntries[cacheKey] = resolved;
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _options.ParsedSourceMapCacheLifetime,
                Size = Math.Max(1, resolved.Document.EstimatedMemorySize)
            }.RegisterPostEvictionCallback(
                static (key, _, _, state) =>
                {
                    if (key is string evictedCacheKey && state is ParsedSourceMapCacheRegistration registration)
                        registration.Service.RemoveParsedSourceMapEntry(evictedCacheKey, registration.SourceMap);
                },
                new ParsedSourceMapCacheRegistration(this, resolved));
            _parsedSourceMaps.Set(cacheKey, resolved, cacheOptions);
        }

        return resolved;
    }

    private async Task RemoveInflightSourceMapWhenCompleteAsync(
        string cacheKey,
        Lazy<Task<ResolvedSourceMap?>> lazy,
        Task<ResolvedSourceMap?> loadTask)
    {
        try
        {
            await loadTask;
        }
        catch
        {
            // The caller observes the load failure; this continuation only releases the in-flight entry.
        }

        RemoveInflightSourceMap(cacheKey, lazy);
    }

    private void RemoveInflightSourceMap(string cacheKey, Lazy<Task<ResolvedSourceMap?>> lazy)
    {
        ICollection<KeyValuePair<string, Lazy<Task<ResolvedSourceMap?>>>> entries = _inflightSourceMaps;
        entries.Remove(new KeyValuePair<string, Lazy<Task<ResolvedSourceMap?>>>(cacheKey, lazy));
    }

    private void RemoveParsedSourceMapEntry(string cacheKey, ResolvedSourceMap sourceMap)
    {
        ICollection<KeyValuePair<string, ResolvedSourceMap>> entries = _parsedSourceMapEntries;
        entries.Remove(new KeyValuePair<string, ResolvedSourceMap>(cacheKey, sourceMap));
    }

    private async Task WaitForInflightSourceMapAsync(string cacheKey, CancellationToken cancellationToken)
    {
        if (_inflightSourceMaps.TryGetValue(cacheKey, out var sourceMap))
            await WaitForInflightSourceMapAsync(sourceMap, cancellationToken);
    }

    private async Task WaitForInflightArtifactAsync(string projectId, string artifactId, CancellationToken cancellationToken)
    {
        string cacheKeyPrefix = projectId + ':';
        var sourceMaps = _inflightSourceMaps
            .Where(entry => entry.Key.StartsWith(cacheKeyPrefix, StringComparison.Ordinal)
                && GetArtifactId(entry.Key[cacheKeyPrefix.Length..]) == artifactId)
            .Select(entry => entry.Value)
            .ToArray();

        foreach (var sourceMap in sourceMaps)
            await WaitForInflightSourceMapAsync(sourceMap, cancellationToken);
    }

    private static async Task WaitForInflightSourceMapAsync(Lazy<Task<ResolvedSourceMap?>> sourceMap, CancellationToken cancellationToken)
    {
        try
        {
            await sourceMap.Value.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // A failed lookup must not prevent an explicit upload or delete from repairing the artifact.
        }
    }

    private async Task<ResolvedSourceMap?> LoadSourceMapAsync(SourceMapRequest request, Uri generatedFileUri, long cacheVersion)
    {
        string projectId = request.ProjectId;
        string generatedFileUrl = generatedFileUri.AbsoluteUri;
        string artifactId = GetArtifactId(generatedFileUrl);
        var stored = await _storage.GetAsync(projectId, artifactId, _options.MaximumSourceMapSize, CancellationToken.None);
        bool refreshStoredMap = stored is not null && ShouldRefresh(stored.Artifact, generatedFileUri);
        if (stored is not null && !refreshStoredMap)
            return Resolve(stored.Artifact, stored.Content, cacheVersion);

        if (!_options.EnableAutoDownload || !String.Equals(generatedFileUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return null;

        string failureCacheKey = GetFailureCacheKey(projectId, generatedFileUrl);
        if ((await _cache.GetAsync<bool>(failureCacheKey)).HasValue)
            return null;

        try
        {
            using var timeoutCancellationTokenSource = new CancellationTokenSource(_options.RequestTimeout);
            await using var artifactLock = await _lockProvider.TryAcquireAsync(
                GetArtifactLockKey(projectId, artifactId),
                TimeSpan.FromSeconds(30),
                timeoutCancellationTokenSource.Token);
            if (artifactLock is null)
                return await CacheFailureAsync(failureCacheKey);

            stored = await _storage.GetAsync(projectId, artifactId, _options.MaximumSourceMapSize, CancellationToken.None);
            if (stored is not null && !ShouldRefresh(stored.Artifact, generatedFileUri))
                return Resolve(stored.Artifact, stored.Content, await GetProjectCacheVersionAsync(projectId));

            if (stored is null && !await _throttle.TryReserveDiscoveryAsync(request))
                return null;

            if (!await _downloadSemaphore.WaitAsync(TimeSpan.Zero, timeoutCancellationTokenSource.Token))
                return null;
            SourceMapDownloader.DownloadedSourceMap? downloaded;
            try
            {
                await using var globalDownloadSlot = await TryAcquireGlobalDownloadSlotAsync(artifactId, timeoutCancellationTokenSource.Token);
                if (globalDownloadSlot is null)
                    return null;

                downloaded = await _downloader.DownloadAsync(generatedFileUri, stored is not null, timeoutCancellationTokenSource.Token);
                if (downloaded is null)
                    return await CacheFailureAsync(failureCacheKey);
            }
            finally
            {
                _downloadSemaphore.Release();
            }

            var artifact = new SourceMapArtifact
            {
                Id = artifactId,
                GeneratedFileUrl = generatedFileUrl,
                SourceMapUrl = downloaded.SourceMapUrl,
                FileName = GetDownloadedFileName(downloaded.SourceMapUrl),
                Size = downloaded.Content.LongLength,
                IsAutoDownloaded = true,
                CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime
            };
            var document = downloaded.Document ?? SourceMapDocument.Parse(downloaded.Content, _options.MaximumMappingSegments);
            await using var projectLock = await TryAcquireProjectStorageLockAsync(projectId, timeoutCancellationTokenSource.Token);
            if (projectLock is null)
                return await CacheFailureAsync(failureCacheKey);

            await ValidateStorageLimitAsync(projectId, artifact, request.IsFreePlan, timeoutCancellationTokenSource.Token);
            await _storage.SaveAsync(projectId, artifact, downloaded.Content, CancellationToken.None);
            long updatedCacheVersion = await ClearCachesAsync(projectId, generatedFileUrl);
            return new ResolvedSourceMap(artifact, document, updatedCacheVersion);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "Timed out downloading a source map for {GeneratedFileUrl}.", generatedFileUrl);
            return await CacheFailureAsync(failureCacheKey);
        }
        catch (SourceMapRequestThrottledException)
        {
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidOperationException or FormatException)
        {
            _logger.LogWarning(ex, "Unable to download a source map for {GeneratedFileUrl}.", generatedFileUrl);
            return await CacheFailureAsync(failureCacheKey);
        }
    }

    private ResolvedSourceMap Resolve(SourceMapArtifact artifact, byte[] content, long cacheVersion)
        => new(artifact, SourceMapDocument.Parse(content, _options.MaximumMappingSegments), cacheVersion);

    private bool ShouldRefresh(SourceMapArtifact artifact, Uri generatedFileUri)
        => artifact.IsAutoDownloaded
            && _options.EnableAutoDownload
            && String.Equals(generatedFileUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && _timeProvider.GetUtcNow().UtcDateTime - artifact.CreatedUtc >= _options.AutoDownloadRefreshInterval;

    private async Task<long> ClearCachesAsync(string projectId, string generatedFileUrl)
    {
        _parsedSourceMaps.Remove(GetMemoryCacheKey(projectId, generatedFileUrl));
        await _cache.RemoveAsync(GetFailureCacheKey(projectId, generatedFileUrl));
        return await AdvanceProjectCacheVersionAsync(projectId);
    }

    private Task<long> GetProjectCacheVersionAsync(string projectId)
        => _cache.GetAsync<long>(GetProjectCacheVersionKey(projectId), 0);

    private async Task<long> AdvanceProjectCacheVersionAsync(string projectId)
    {
        string cacheKey = GetProjectCacheVersionKey(projectId);
        long cacheVersion = await _cache.IncrementAsync(cacheKey, 1);
        if (!await _cache.SetAsync(cacheKey, cacheVersion))
            throw new IOException("Unable to persist the source map cache generation.");
        return cacheVersion;
    }

    private Task<ILock?> TryAcquireProjectStorageLockAsync(string projectId, CancellationToken cancellationToken)
        => _lockProvider.TryAcquireAsync(GetProjectStorageLockKey(projectId), TimeSpan.FromSeconds(30), cancellationToken);

    public async Task SaveUsagesAsync(CancellationToken cancellationToken = default)
    {
        string pendingCacheKey = GetUsagePendingCacheKey();
        var pending = await _cache.GetListAsync<SourceMapUsageKey>(pendingCacheKey);
        if (!pending.HasValue)
            return;

        foreach (var usage in pending.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _cache.ListRemoveAsync(pendingCacheKey, usage);

            try
            {
                DateTime lastUsedUtc = await GetPendingLastUsedUtcAsync(usage.ProjectId, usage.ArtifactId);
                if (lastUsedUtc == DateTime.MinValue)
                    continue;

                await using var projectLock = await TryAcquireProjectStorageLockAsync(usage.ProjectId, cancellationToken);
                if (projectLock is null)
                {
                    await _cache.ListAddAsync(pendingCacheKey, usage);
                    continue;
                }

                var result = await _storage.SetLastUsedUtcAsync(usage.ProjectId, usage.ArtifactId, lastUsedUtc, cancellationToken);
                if (result == SourceMapStorage.SetLastUsedResult.Failed)
                    await _cache.ListAddAsync(pendingCacheKey, usage);
                else if (result == SourceMapStorage.SetLastUsedResult.NotFound)
                    await _cache.RemoveAsync(GetLastUsedCacheKey(usage.ProjectId, usage.ArtifactId));
            }
            catch
            {
                await _cache.ListAddAsync(pendingCacheKey, usage);
                throw;
            }
        }
    }

    public async Task<int> CleanupStaleArtifactsAsync(string projectId, bool isFreePlan, CancellationToken cancellationToken = default)
    {
        await using var projectLock = await TryAcquireProjectStorageLockAsync(projectId, cancellationToken);
        if (projectLock is null)
            throw new IOException("Unable to acquire the project source map storage lock.");

        return await CleanupStaleArtifactsUnderLockAsync(projectId, isFreePlan, cancellationToken);
    }

    private async Task ValidateStorageLimitAsync(string projectId, SourceMapArtifact artifact, bool isFreePlan, CancellationToken cancellationToken)
    {
        await CleanupStaleArtifactsUnderLockAsync(projectId, isFreePlan, cancellationToken);
        var usage = await _storage.GetProjectStorageUsageAsync(projectId, artifact.Id, cancellationToken);
        var otherArtifacts = usage.Artifacts.Where(existing => !String.Equals(existing.Id, artifact.Id, StringComparison.Ordinal)).ToArray();
        int maximumArtifacts = isFreePlan ? _options.MaximumArtifactsPerFreeProject : _options.MaximumArtifactsPerProject;
        long maximumStorageSize = isFreePlan ? _options.MaximumStorageSizePerFreeProject : _options.MaximumStorageSizePerProject;
        if (otherArtifacts.Length >= maximumArtifacts)
            throw new SourceMapStorageLimitException($"The project source map artifact limit of {maximumArtifacts:N0} has been reached.");

        if (usage.RetainedBytes > maximumStorageSize
            || artifact.Size > maximumStorageSize - usage.RetainedBytes)
            throw new SourceMapStorageLimitException("The project source map storage limit has been reached.");
    }

    private async Task TrackUsageAsync(string projectId, string artifactId)
    {
        string localCacheKey = $"{projectId}:{artifactId}";
        if (_recentlyTrackedUsages.TryGetValue(localCacheKey, out _))
            return;

        _recentlyTrackedUsages.Set(localCacheKey, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _options.UsageTrackingDebounce,
            Size = 1
        });

        var usage = new SourceMapUsageKey(projectId, artifactId);
        try
        {
            await Task.WhenAll(
                _cache.ListAddAsync(GetUsagePendingCacheKey(), usage),
                _cache.SetIfHigherAsync(GetLastUsedCacheKey(projectId, artifactId), _timeProvider.GetUtcNow().UtcDateTime, _usageCacheLifetime));
        }
        catch (Exception ex)
        {
            _recentlyTrackedUsages.Remove(localCacheKey);
            _logger.LogWarning(ex, "Unable to queue source map usage for project {ProjectId} artifact {SourceMapArtifactId}.", projectId, artifactId);
        }
    }

    private async Task<int> CleanupStaleArtifactsUnderLockAsync(string projectId, bool isFreePlan, CancellationToken cancellationToken)
    {
        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        DateTime cutoffUtc = nowUtc - (isFreePlan ? _options.FreeArtifactRetention : _options.ArtifactRetention);
        var artifacts = await _storage.GetArtifactsAsync(projectId, cancellationToken);
        int removed = 0;

        foreach (var artifact in artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTime pendingLastUsedUtc = await GetPendingLastUsedUtcAsync(projectId, artifact.Id);
            DateTime effectiveLastUsedUtc = artifact.LastUsedUtc ?? artifact.CreatedUtc;
            if (pendingLastUsedUtc > effectiveLastUsedUtc)
            {
                effectiveLastUsedUtc = pendingLastUsedUtc;
                if (await _storage.SetLastUsedUtcAsync(projectId, artifact.Id, effectiveLastUsedUtc, cancellationToken) != SourceMapStorage.SetLastUsedResult.Updated)
                    throw new IOException("Unable to update source map usage metadata.");
            }

            if (effectiveLastUsedUtc > cutoffUtc)
                continue;

            var deleted = await _storage.DeleteAsync(projectId, artifact.Id, cancellationToken);
            if (deleted is null)
                continue;

            await RemoveUsageTrackingAsync(projectId, artifact.Id);
            await ClearCachesAsync(projectId, artifact.GeneratedFileUrl);
            removed++;
        }

        return removed;
    }

    private Task<DateTime> GetPendingLastUsedUtcAsync(string projectId, string artifactId)
        => _cache.GetUnixTimeMillisecondsAsync(GetLastUsedCacheKey(projectId, artifactId), DateTime.MinValue);

    private Task RemoveUsageTrackingAsync(string projectId, string artifactId)
    {
        _recentlyTrackedUsages.Remove($"{projectId}:{artifactId}");
        return Task.WhenAll(
            _cache.ListRemoveAsync(GetUsagePendingCacheKey(), new SourceMapUsageKey(projectId, artifactId)),
            _cache.RemoveAsync(GetLastUsedCacheKey(projectId, artifactId)));
    }

    private async Task<ResolvedSourceMap?> CacheFailureAsync(string failureCacheKey)
    {
        await _cache.SetAsync(failureCacheKey, true, FailureCacheLifetime);
        return null;
    }

    private async Task<ILock?> TryAcquireGlobalDownloadSlotAsync(string artifactId, CancellationToken cancellationToken)
    {
        int start = (int)(Convert.ToUInt32(artifactId[..8], 16) % _options.MaximumConcurrentDownloadsGlobally);
        for (int offset = 0; offset < _options.MaximumConcurrentDownloadsGlobally; offset++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int slot = (start + offset) % _options.MaximumConcurrentDownloadsGlobally;
            var globalDownloadSlot = await _lockProvider.TryAcquireAsync(
                $"source-maps:download-slot:{slot}",
                _options.RequestTimeout + TimeSpan.FromSeconds(1),
                TimeSpan.Zero);
            if (globalDownloadSlot is not null)
                return globalDownloadSlot;
        }

        return null;
    }

    public static bool TryNormalizeGeneratedFileUrl(string? value, bool requireHttps, out Uri uri)
    {
        uri = null!;
        if (String.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsedUri))
            return false;

        bool validScheme = String.Equals(parsedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || (!requireHttps && String.Equals(parsedUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase));
        if (!validScheme || !String.IsNullOrEmpty(parsedUri.UserInfo) || !String.IsNullOrEmpty(parsedUri.Fragment))
            return false;

        uri = parsedUri;
        return true;
    }

    private static bool IsArtifactId(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
    private static string GetArtifactId(string generatedFileUrl) => generatedFileUrl.ToSHA256();
    private static string GetArtifactLockKey(string projectId, string artifactId) => $"source-maps:artifact:{projectId}:{artifactId}";
    private static string GetProjectStorageLockKey(string projectId) => $"source-maps:project-storage:{projectId}";
    private static string GetProjectCacheVersionKey(string projectId) => $"source-maps:cache-version:{projectId}";
    private static string GetMemoryCacheKey(string projectId, string generatedFileUrl) => $"{projectId}:{generatedFileUrl}";
    private static string GetFailureCacheKey(string projectId, string generatedFileUrl) => $"source-maps:failure:{projectId}:{generatedFileUrl.ToSHA256()}";
    private static string GetUsagePendingCacheKey() => "source-maps:usage:pending";
    private static string GetLastUsedCacheKey(string projectId, string artifactId) => $"source-maps:usage:last:{projectId}:{artifactId}";

    private static string? GetDownloadedFileName(string? sourceMapUrl)
    {
        if (sourceMapUrl is null || !Uri.TryCreate(sourceMapUrl, UriKind.Absolute, out var uri))
            return null;
        return Path.GetFileName(uri.AbsolutePath);
    }

    private sealed record ResolvedSourceMap(SourceMapArtifact Artifact, SourceMapDocument Document, long CacheVersion);
    private sealed record ParsedSourceMapCacheRegistration(SourceMapService Service, ResolvedSourceMap SourceMap);
}

internal sealed record SourceMapUsageKey(string ProjectId, string ArtifactId);

public sealed class SourceMapStorageLimitException(string message) : InvalidOperationException(message);
