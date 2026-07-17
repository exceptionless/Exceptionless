using System.Security.Cryptography;
using System.Text.Json;
using Foundatio.Storage;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services.SourceMaps;

internal sealed class SourceMapStorage
{
    private const string RootPath = "source-maps";
    private readonly IFileStorage _storage;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ILogger _logger;

    public SourceMapStorage(IFileStorage storage, JsonSerializerOptions serializerOptions, ILogger logger)
    {
        _storage = storage;
        _serializerOptions = serializerOptions;
        _logger = logger;
    }

    public async Task SaveAsync(string projectId, SourceMapArtifact artifact, byte[] sourceMap, CancellationToken cancellationToken)
    {
        string metadataPath = GetMetadataPath(projectId, artifact.Id);
        var previousMetadata = await ReadMetadataAsync(metadataPath, cancellationToken);
        string mapFileName = $"{artifact.Id}-{Convert.ToHexString(SHA256.HashData(sourceMap)).ToLowerInvariant()}.map";
        string mapPath = GetMapPath(projectId, mapFileName);

        await using (var mapStream = new MemoryStream(sourceMap, writable: false))
        {
            if (!await _storage.SaveFileAsync(mapPath, mapStream, cancellationToken))
                throw new IOException("Unable to save the source map.");
        }

        try
        {
            var metadata = new StoredSourceMapMetadata(artifact, mapFileName);
            if (!await SaveMetadataAsync(metadataPath, metadata, cancellationToken))
                throw new IOException("Unable to save the source map metadata.");

            if (previousMetadata is not null
                && previousMetadata.MapFileName != mapFileName
                && !await DeleteAndVerifyAsync(GetMapPath(projectId, previousMetadata.MapFileName), cancellationToken))
            {
                bool metadataRestored = await SaveMetadataAsync(metadataPath, previousMetadata, CancellationToken.None);
                bool newMapDeleted = await DeleteAndVerifyAsync(mapPath, CancellationToken.None);
                if (!metadataRestored || !newMapDeleted)
                    _logger.LogError("Unable to roll back a failed source map replacement for project {ProjectId} and artifact {SourceMapArtifactId}.", projectId, artifact.Id);
                throw new IOException("Unable to remove the superseded source map.");
            }
        }
        catch
        {
            if (previousMetadata?.MapFileName != mapFileName)
                await DeleteAndVerifyAsync(mapPath, CancellationToken.None);
            throw;
        }
    }

    public async Task<ProjectStorageUsage> GetProjectStorageUsageAsync(string projectId, string replacementArtifactId, CancellationToken cancellationToken)
    {
        var metadataFiles = await _storage.GetFileListAsync($"{RootPath}/{projectId}/*.json", cancellationToken: cancellationToken);
        var artifacts = new List<SourceMapArtifact>(metadataFiles.Count);
        var referencedMapPaths = new HashSet<string>(StringComparer.Ordinal);
        string? replacementMapPath = null;
        foreach (var metadataFile in metadataFiles)
        {
            try
            {
                var metadata = await ReadMetadataAsync(metadataFile.Path, cancellationToken);
                if (metadata is null || !IsValidMapFileName(metadata.Artifact.Id, metadata.MapFileName))
                    continue;

                string mapPath = GetMapPath(projectId, metadata.MapFileName);
                referencedMapPaths.Add(mapPath);
                artifacts.Add(metadata.Artifact);
                if (String.Equals(metadata.Artifact.Id, replacementArtifactId, StringComparison.Ordinal))
                    replacementMapPath = mapPath;
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                _logger.LogWarning(ex, "Unable to read source map metadata {SourceMapMetadataPath} while calculating storage usage.", metadataFile.Path);
            }
        }

        var mapFiles = await _storage.GetFileListAsync($"{RootPath}/{projectId}/*.map", cancellationToken: cancellationToken);
        long retainedBytes = 0;
        foreach (var mapFile in mapFiles)
        {
            if (!referencedMapPaths.Contains(mapFile.Path) && await DeleteAndVerifyAsync(mapFile.Path, cancellationToken))
                continue;

            if (!String.Equals(mapFile.Path, replacementMapPath, StringComparison.Ordinal))
            {
                long fileSize = Math.Max(0, mapFile.Size);
                retainedBytes = fileSize > Int64.MaxValue - retainedBytes ? Int64.MaxValue : retainedBytes + fileSize;
            }
        }

        return new ProjectStorageUsage(artifacts, retainedBytes);
    }

    public async Task<StoredSourceMap?> GetAsync(string projectId, string artifactId, int maximumBytes, CancellationToken cancellationToken)
    {
        var metadata = await ReadMetadataAsync(GetMetadataPath(projectId, artifactId), cancellationToken);
        if (metadata is null || !IsValidMapFileName(artifactId, metadata.MapFileName))
            return null;

        string mapPath = GetMapPath(projectId, metadata.MapFileName);
        if (!await _storage.ExistsAsync(mapPath))
            return null;

        await using var stream = await _storage.GetFileStreamAsync(mapPath, StreamMode.Read, cancellationToken);
        if (stream is null)
            return null;

        byte[] content = await SourceMapContent.ReadLimitedAsync(stream, maximumBytes, cancellationToken);
        return new StoredSourceMap(metadata.Artifact, content);
    }

    public async Task<IReadOnlyCollection<SourceMapArtifact>> GetArtifactsAsync(string projectId, CancellationToken cancellationToken)
    {
        var files = await _storage.GetFileListAsync($"{RootPath}/{projectId}/*.json", cancellationToken: cancellationToken);
        var artifacts = new List<SourceMapArtifact>(files.Count);
        foreach (var file in files)
        {
            try
            {
                var metadata = await ReadMetadataAsync(file.Path, cancellationToken);
                if (metadata is not null)
                    artifacts.Add(metadata.Artifact);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                _logger.LogWarning(ex, "Unable to read source map metadata {SourceMapMetadataPath}.", file.Path);
            }
        }

        return artifacts.OrderByDescending(artifact => artifact.CreatedUtc).ToArray();
    }

    public async Task<SourceMapArtifact?> DeleteAsync(string projectId, string artifactId, CancellationToken cancellationToken)
    {
        string metadataPath = GetMetadataPath(projectId, artifactId);
        var metadata = await ReadMetadataAsync(metadataPath, cancellationToken);
        if (!await DeleteAndVerifyAsync(metadataPath, cancellationToken))
            return null;

        if (metadata is not null && IsValidMapFileName(artifactId, metadata.MapFileName))
        {
            string mapPath = GetMapPath(projectId, metadata.MapFileName);
            if (!await DeleteAndVerifyAsync(mapPath, cancellationToken))
                _logger.LogWarning("Unable to remove deleted source map content {SourceMapPath}.", mapPath);
        }
        else
            await _storage.DeleteFilesAsync($"{RootPath}/{projectId}/{artifactId}-*.map", cancellationToken);

        return metadata?.Artifact;
    }

    public Task DeleteAllAsync(string projectId, CancellationToken cancellationToken)
        => _storage.DeleteFilesAsync($"{RootPath}/{projectId}/*", cancellationToken);

    private async Task<StoredSourceMapMetadata?> ReadMetadataAsync(string path, CancellationToken cancellationToken)
    {
        if (!await _storage.ExistsAsync(path))
            return null;

        await using var stream = await _storage.GetFileStreamAsync(path, StreamMode.Read, cancellationToken);
        return stream is null
            ? null
            : await JsonSerializer.DeserializeAsync<StoredSourceMapMetadata>(stream, _serializerOptions, cancellationToken);
    }

    private async Task<bool> SaveMetadataAsync(string path, StoredSourceMapMetadata metadata, CancellationToken cancellationToken)
    {
        byte[] metadataBytes = JsonSerializer.SerializeToUtf8Bytes(metadata, _serializerOptions);
        await using var metadataStream = new MemoryStream(metadataBytes, writable: false);
        return await _storage.SaveFileAsync(path, metadataStream, cancellationToken);
    }

    private async Task<bool> DeleteAndVerifyAsync(string path, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            await _storage.DeleteFileAsync(path, cancellationToken);
            if (!await _storage.ExistsAsync(path))
                return true;
        }

        return false;
    }

    private static bool IsValidMapFileName(string artifactId, string mapFileName)
        => mapFileName.Length == artifactId.Length + 1 + 64 + 4
            && mapFileName.StartsWith(artifactId + '-', StringComparison.Ordinal)
            && mapFileName.EndsWith(".map", StringComparison.Ordinal)
            && mapFileName.AsSpan(artifactId.Length + 1, 64).ToArray().All(character => Uri.IsHexDigit(character));

    private static string GetMetadataPath(string projectId, string artifactId) => $"{RootPath}/{projectId}/{artifactId}.json";
    private static string GetMapPath(string projectId, string mapFileName) => $"{RootPath}/{projectId}/{mapFileName}";

    internal sealed record StoredSourceMap(SourceMapArtifact Artifact, byte[] Content);
    internal sealed record ProjectStorageUsage(IReadOnlyCollection<SourceMapArtifact> Artifacts, long RetainedBytes);
    private sealed record StoredSourceMapMetadata(SourceMapArtifact Artifact, string MapFileName);
}
