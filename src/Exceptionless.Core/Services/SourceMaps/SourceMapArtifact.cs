namespace Exceptionless.Core.Services.SourceMaps;

public sealed record SourceMapArtifact
{
    public required string Id { get; init; }
    public required string GeneratedFileUrl { get; init; }
    public string? SourceMapUrl { get; init; }
    public string? FileName { get; init; }
    public required long Size { get; init; }
    public required bool IsAutoDownloaded { get; init; }
    public required DateTime CreatedUtc { get; init; }
    public DateTime? LastUsedUtc { get; init; }
}
