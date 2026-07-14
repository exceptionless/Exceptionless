using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Caching;
using Foundatio.Storage;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services.SourceMaps;

public sealed class SourceMapService
{
    public const string HttpClientName = "SourceMaps";
    public const int MaximumUploadRequestSize = 21 * 1024 * 1024;
    private const string SourceMapDataKey = "@source_map";
    private static readonly TimeSpan FailureCacheLifetime = TimeSpan.FromMinutes(15);
    private readonly SemaphoreSlim _downloadSemaphore;
    private readonly ConcurrentDictionary<string, Lazy<Task<ResolvedSourceMap?>>> _sourceMaps = new(StringComparer.Ordinal);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFileStorage _storage;
    private readonly ICacheClient _cache;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly SourceMapOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SourceMapService> _logger;

    public SourceMapService(
        IHttpClientFactory httpClientFactory,
        IFileStorage storage,
        ICacheClient cache,
        JsonSerializerOptions serializerOptions,
        AppOptions options,
        TimeProvider timeProvider,
        ILogger<SourceMapService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _storage = storage;
        _cache = cache;
        _serializerOptions = serializerOptions;
        _options = options.SourceMapOptions;
        _downloadSemaphore = new SemaphoreSlim(Math.Max(1, _options.MaximumConcurrentDownloads));
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<SourceMapArtifact> SaveUploadedAsync(string projectId, string generatedFileUrl, string? fileName, Stream stream, CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeGeneratedFileUrl(generatedFileUrl, requireHttps: false, out var generatedFileUri))
            throw new ArgumentException("The generated file URL must be an absolute HTTP or HTTPS URL without credentials or a fragment.", nameof(generatedFileUrl));

        byte[] sourceMap = await ReadLimitedAsync(stream, _options.MaximumSourceMapSize, cancellationToken);
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

        await SaveArtifactAsync(projectId, artifact, sourceMap, cancellationToken);
        await ClearCachesAsync(projectId, normalizedUrl);
        return artifact;
    }

    public async Task<IReadOnlyCollection<SourceMapArtifact>> GetArtifactsAsync(string projectId, CancellationToken cancellationToken = default)
    {
        string pattern = $"source-maps/{projectId}/*.json";
        var files = await _storage.GetFileListAsync(pattern, cancellationToken: cancellationToken);
        var artifacts = new List<SourceMapArtifact>(files.Count);
        foreach (var file in files)
        {
            try
            {
                await using var stream = await _storage.GetFileStreamAsync(file.Path, StreamMode.Read, cancellationToken);
                if (stream is null)
                    continue;

                var artifact = await JsonSerializer.DeserializeAsync<SourceMapArtifact>(stream, _serializerOptions, cancellationToken);
                if (artifact is not null)
                    artifacts.Add(artifact);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                _logger.LogWarning(ex, "Unable to read source map metadata {SourceMapMetadataPath}.", file.Path);
            }
        }

        return artifacts.OrderByDescending(artifact => artifact.CreatedUtc).ToArray();
    }

    public async Task<bool> DeleteArtifactAsync(string projectId, string artifactId, CancellationToken cancellationToken = default)
    {
        if (!IsArtifactId(artifactId))
            return false;

        string metadataPath = GetMetadataPath(projectId, artifactId);
        SourceMapArtifact? artifact = await ReadArtifactMetadataAsync(metadataPath, cancellationToken);
        bool mapDeleted = await _storage.DeleteFileAsync(GetMapPath(projectId, artifactId), cancellationToken);
        bool metadataDeleted = await _storage.DeleteFileAsync(metadataPath, cancellationToken);
        if (artifact is not null)
            await ClearCachesAsync(projectId, artifact.GeneratedFileUrl);

        return mapDeleted || metadataDeleted;
    }

    public async Task DeleteAllArtifactsAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await _storage.DeleteFilesAsync($"source-maps/{projectId}/*", cancellationToken);
        foreach (string key in _sourceMaps.Keys.Where(key => key.StartsWith(projectId + ':', StringComparison.Ordinal)))
            _sourceMaps.TryRemove(key, out _);
    }

    public async Task<bool> SymbolicateAsync(string projectId, InnerError? error, CancellationToken cancellationToken = default)
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

                        if (await SymbolicateFrameAsync(projectId, frame, processingCancellationTokenSource.Token))
                            changed = true;
                    }
                }

                error = error.Inner;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Source map processing exceeded its time budget for project {ProjectId}.", projectId);
        }

        return changed;
    }

    private async Task<bool> SymbolicateFrameAsync(string projectId, StackFrame frame, CancellationToken cancellationToken)
    {
        if (frame.Data?.ContainsKey(SourceMapDataKey) == true || frame.LineNumber is null || frame.LineNumber < 1 || String.IsNullOrWhiteSpace(frame.FileName))
            return false;

        if (!TryNormalizeGeneratedFileUrl(frame.FileName, requireHttps: false, out var generatedFileUri))
            return false;

        var resolved = await GetSourceMapAsync(projectId, generatedFileUri, cancellationToken);
        if (resolved is null)
            return false;

        int generatedColumn = frame.Column.GetValueOrDefault(1);
        if (generatedColumn > 0)
            generatedColumn--;

        var original = resolved.Document.FindOriginalLocation(frame.LineNumber.Value - 1, generatedColumn);
        if (original is null)
            return false;

        frame.Data ??= new DataDictionary();
        frame.Data[SourceMapDataKey] = new DataDictionary
        {
            ["generated_file_name"] = frame.FileName,
            ["generated_line_number"] = frame.LineNumber,
            ["generated_column"] = frame.Column,
            ["source_map_id"] = resolved.Artifact.Id
        };
        frame.FileName = original.Source;
        frame.LineNumber = original.Line + 1;
        frame.Column = original.Column + 1;
        if (!String.IsNullOrWhiteSpace(original.Name))
            frame.Name = original.Name;

        return true;
    }

    private async Task<ResolvedSourceMap?> GetSourceMapAsync(string projectId, Uri generatedFileUri, CancellationToken cancellationToken)
    {
        string normalizedUrl = generatedFileUri.AbsoluteUri;
        string cacheKey = GetMemoryCacheKey(projectId, normalizedUrl);
        var lazy = _sourceMaps.GetOrAdd(cacheKey, _ => new Lazy<Task<ResolvedSourceMap?>>(
            () => LoadSourceMapAsync(projectId, generatedFileUri), LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            var result = await lazy.Value.WaitAsync(cancellationToken);
            if (result is null)
                _sourceMaps.TryRemove(cacheKey, out _);

            return result;
        }
        catch
        {
            _sourceMaps.TryRemove(cacheKey, out _);
            throw;
        }
    }

    private async Task<ResolvedSourceMap?> LoadSourceMapAsync(string projectId, Uri generatedFileUri)
    {
        string generatedFileUrl = generatedFileUri.AbsoluteUri;
        string artifactId = GetArtifactId(generatedFileUrl);
        string mapPath = GetMapPath(projectId, artifactId);
        var artifact = await ReadArtifactMetadataAsync(GetMetadataPath(projectId, artifactId), CancellationToken.None);
        if (artifact is not null && await _storage.ExistsAsync(mapPath))
        {
            byte[] storedSourceMap = await ReadStorageFileAsync(mapPath, _options.MaximumSourceMapSize, CancellationToken.None);
            return new ResolvedSourceMap(artifact, SourceMapDocument.Parse(storedSourceMap, _options.MaximumMappingSegments));
        }

        if (!_options.EnableAutoDownload || !String.Equals(generatedFileUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return null;

        string failureCacheKey = GetFailureCacheKey(projectId, generatedFileUrl);
        if ((await _cache.GetAsync<bool>(failureCacheKey)).HasValue)
            return null;

        try
        {
            using var timeoutCancellationTokenSource = new CancellationTokenSource(_options.RequestTimeout);
            if (!await TryReserveAutoDownloadAsync(projectId))
                return await CacheFailureAsync(failureCacheKey);

            await _downloadSemaphore.WaitAsync(timeoutCancellationTokenSource.Token);
            DownloadedSourceMap? downloaded;
            try
            {
                downloaded = await DownloadSourceMapAsync(generatedFileUri, timeoutCancellationTokenSource.Token);
                if (downloaded is null)
                    return await CacheFailureAsync(failureCacheKey);
            }
            finally
            {
                _downloadSemaphore.Release();
            }

            var downloadedArtifact = new SourceMapArtifact
            {
                Id = artifactId,
                GeneratedFileUrl = generatedFileUrl,
                SourceMapUrl = downloaded.SourceMapUrl,
                FileName = GetDownloadedFileName(downloaded.SourceMapUrl),
                Size = downloaded.Content.LongLength,
                IsAutoDownloaded = true,
                CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime
            };
            var document = SourceMapDocument.Parse(downloaded.Content, _options.MaximumMappingSegments);
            await SaveArtifactAsync(projectId, downloadedArtifact, downloaded.Content, CancellationToken.None);
            return new ResolvedSourceMap(downloadedArtifact, document);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "Timed out downloading a source map for {GeneratedFileUrl}.", generatedFileUrl);
            return await CacheFailureAsync(failureCacheKey);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidOperationException or FormatException)
        {
            _logger.LogWarning(ex, "Unable to download a source map for {GeneratedFileUrl}.", generatedFileUrl);
            return await CacheFailureAsync(failureCacheKey);
        }
    }

    private async Task<DownloadedSourceMap?> DownloadSourceMapAsync(Uri generatedFileUri, CancellationToken cancellationToken)
    {
        using var generatedResponse = await SendAsync(generatedFileUri, _options.MaximumGeneratedFileSize, request => request.Headers.Range = new RangeHeaderValue(null, 64 * 1024), cancellationToken);
        if (!generatedResponse.Response.IsSuccessStatusCode)
            return null;

        string? sourceMapReference = GetSourceMapHeader(generatedResponse.Response);
        if (String.IsNullOrWhiteSpace(sourceMapReference))
        {
            byte[] generatedContent = await ReadLimitedAsync(await generatedResponse.Response.Content.ReadAsStreamAsync(cancellationToken), _options.MaximumGeneratedFileSize, cancellationToken);
            sourceMapReference = FindSourceMapReference(Encoding.UTF8.GetString(generatedContent));
        }

        if (String.IsNullOrWhiteSpace(sourceMapReference))
        {
            var fallbackUriBuilder = new UriBuilder(generatedResponse.Uri) { Path = generatedResponse.Uri.AbsolutePath + ".map" };
            return await DownloadSourceMapContentAsync(fallbackUriBuilder.Uri, cancellationToken);
        }

        if (sourceMapReference.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return new DownloadedSourceMap(DecodeDataUri(sourceMapReference, _options.MaximumSourceMapSize), null);

        if (!Uri.TryCreate(generatedResponse.Uri, sourceMapReference, out var sourceMapUri) || !IsAutoDownloadUri(sourceMapUri))
            return null;

        return await DownloadSourceMapContentAsync(sourceMapUri, cancellationToken);
    }

    private async Task<DownloadedSourceMap?> DownloadSourceMapContentAsync(Uri sourceMapUri, CancellationToken cancellationToken)
    {
        using var result = await SendAsync(sourceMapUri, _options.MaximumSourceMapSize, null, cancellationToken);
        if (!result.Response.IsSuccessStatusCode)
            return null;

        byte[] content = await ReadLimitedAsync(await result.Response.Content.ReadAsStreamAsync(cancellationToken), _options.MaximumSourceMapSize, cancellationToken);
        return new DownloadedSourceMap(content, result.Uri.AbsoluteUri);
    }

    private async Task<HttpDownloadResult> SendAsync(Uri uri, int maximumBytes, Action<HttpRequestMessage>? configureRequest, CancellationToken cancellationToken)
    {
        Uri currentUri = uri;
        for (int redirectCount = 0; ; redirectCount++)
        {
            if (!IsAutoDownloadUri(currentUri))
                throw new InvalidOperationException("Source map auto-download only supports public HTTPS URLs.");

            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            request.Headers.AcceptEncoding.ParseAdd("gzip, deflate, br");
            configureRequest?.Invoke(request);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.Content.Headers.ContentLength > maximumBytes)
            {
                response.Dispose();
                throw new InvalidOperationException("The downloaded file exceeded the configured maximum size.");
            }

            if (!IsRedirect(response.StatusCode))
                return new HttpDownloadResult(currentUri, response);

            if (redirectCount >= _options.MaximumRedirects || response.Headers.Location is null)
            {
                response.Dispose();
                throw new InvalidOperationException("The source map download exceeded the allowed redirects.");
            }

            Uri redirectUri = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(currentUri, response.Headers.Location);
            response.Dispose();
            currentUri = redirectUri;
        }
    }

    private async Task SaveArtifactAsync(string projectId, SourceMapArtifact artifact, byte[] sourceMap, CancellationToken cancellationToken)
    {
        string mapPath = GetMapPath(projectId, artifact.Id);
        string metadataPath = GetMetadataPath(projectId, artifact.Id);
        await using var mapStream = new MemoryStream(sourceMap, writable: false);
        if (!await _storage.SaveFileAsync(mapPath, mapStream, cancellationToken))
            throw new IOException("Unable to save the source map.");

        try
        {
            byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(artifact, _serializerOptions);
            await using var metadataStream = new MemoryStream(metadata, writable: false);
            if (!await _storage.SaveFileAsync(metadataPath, metadataStream, cancellationToken))
                throw new IOException("Unable to save the source map metadata.");
        }
        catch
        {
            await _storage.DeleteFileAsync(mapPath, CancellationToken.None);
            throw;
        }
    }

    private async Task<SourceMapArtifact?> ReadArtifactMetadataAsync(string path, CancellationToken cancellationToken)
    {
        if (!await _storage.ExistsAsync(path))
            return null;

        await using var stream = await _storage.GetFileStreamAsync(path, StreamMode.Read, cancellationToken);
        return stream is null ? null : await JsonSerializer.DeserializeAsync<SourceMapArtifact>(stream, _serializerOptions, cancellationToken);
    }

    private async Task<byte[]> ReadStorageFileAsync(string path, int maximumBytes, CancellationToken cancellationToken)
    {
        await using var stream = await _storage.GetFileStreamAsync(path, StreamMode.Read, cancellationToken)
            ?? throw new FileNotFoundException("The source map file was not found.", path);
        return await ReadLimitedAsync(stream, maximumBytes, cancellationToken);
    }

    private Task ClearCachesAsync(string projectId, string generatedFileUrl)
    {
        _sourceMaps.TryRemove(GetMemoryCacheKey(projectId, generatedFileUrl), out _);
        return _cache.RemoveAsync(GetFailureCacheKey(projectId, generatedFileUrl));
    }

    private async Task<ResolvedSourceMap?> CacheFailureAsync(string failureCacheKey)
    {
        await _cache.SetAsync(failureCacheKey, true, FailureCacheLifetime);
        return null;
    }

    private async Task<bool> TryReserveAutoDownloadAsync(string projectId)
    {
        string hour = _timeProvider.GetUtcNow().ToString("yyyyMMddHH");
        long count = await _cache.IncrementAsync($"source-maps:downloads:{projectId}:{hour}", 1, TimeSpan.FromHours(2));
        if (count <= _options.MaximumAutoDownloadsPerProjectPerHour)
            return true;

        if (count == _options.MaximumAutoDownloadsPerProjectPerHour + 1)
            _logger.LogWarning("Source map auto-download limit reached for project {ProjectId}.", projectId);
        return false;
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

    private static bool IsAutoDownloadUri(Uri uri) => TryNormalizeGeneratedFileUrl(uri.AbsoluteUri, requireHttps: true, out _);

    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is HttpStatusCode.MovedPermanently
        or HttpStatusCode.Redirect
        or HttpStatusCode.RedirectMethod
        or HttpStatusCode.TemporaryRedirect
        or HttpStatusCode.PermanentRedirect;

    private static string? GetSourceMapHeader(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("SourceMap", out var sourceMapValues))
            return sourceMapValues.FirstOrDefault();
        if (response.Headers.TryGetValues("X-SourceMap", out var legacySourceMapValues))
            return legacySourceMapValues.FirstOrDefault();
        return null;
    }

    private static string? FindSourceMapReference(string generatedContent)
    {
        const string marker = "sourceMappingURL=";
        int markerIndex = generatedContent.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return null;

        int start = markerIndex + marker.Length;
        int end = generatedContent.IndexOfAny(['\r', '\n'], start);
        string value = generatedContent[start..(end < 0 ? generatedContent.Length : end)].Trim();
        if (value.EndsWith("*/", StringComparison.Ordinal))
            value = value[..^2].Trim();
        return value;
    }

    private static byte[] DecodeDataUri(string value, int maximumBytes)
    {
        int commaIndex = value.IndexOf(',');
        if (commaIndex < 0)
            throw new FormatException("The inline source map data URI is invalid.");

        string metadata = value[..commaIndex];
        string data = value[(commaIndex + 1)..];
        byte[] decoded = metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase)
            ? Convert.FromBase64String(data)
            : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(data));
        if (decoded.Length > maximumBytes)
            throw new InvalidOperationException("The inline source map exceeded the configured maximum size.");
        return decoded;
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream stream, int maximumBytes, CancellationToken cancellationToken)
    {
        var memoryStream = new MemoryStream(Math.Min(maximumBytes, 64 * 1024));
        byte[] buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memoryStream.Length + read > maximumBytes)
                throw new InvalidOperationException("The file exceeded the configured maximum size.");
            await memoryStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return memoryStream.ToArray();
    }

    private static bool IsArtifactId(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
    private static string GetArtifactId(string generatedFileUrl) => generatedFileUrl.ToSHA256();
    private static string GetMapPath(string projectId, string artifactId) => $"source-maps/{projectId}/{artifactId}.map";
    private static string GetMetadataPath(string projectId, string artifactId) => $"source-maps/{projectId}/{artifactId}.json";
    private static string GetMemoryCacheKey(string projectId, string generatedFileUrl) => $"{projectId}:{generatedFileUrl}";
    private static string GetFailureCacheKey(string projectId, string generatedFileUrl) => $"source-maps:failure:{projectId}:{generatedFileUrl.ToSHA256()}";

    private static string? GetDownloadedFileName(string? sourceMapUrl)
    {
        if (sourceMapUrl is null || !Uri.TryCreate(sourceMapUrl, UriKind.Absolute, out var uri))
            return null;
        return Path.GetFileName(uri.AbsolutePath);
    }

    private sealed record DownloadedSourceMap(byte[] Content, string? SourceMapUrl);
    private sealed record ResolvedSourceMap(SourceMapArtifact Artifact, SourceMapDocument Document);

    private sealed record HttpDownloadResult(Uri Uri, HttpResponseMessage Response) : IDisposable
    {
        public void Dispose() => Response.Dispose();
    }
}
