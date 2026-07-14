using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public sealed class SourceMapOptions
{
    public bool EnableAutoDownload { get; internal set; }
    public int RequestTimeoutMilliseconds { get; internal set; }
    public int MaximumGeneratedFileSize { get; internal set; }
    public int MaximumSourceMapSize { get; internal set; }
    public int MaximumMappingSegments { get; internal set; }
    public int MaximumRedirects { get; internal set; }
    public int MaximumConcurrentDownloads { get; internal set; }
    public int MaximumAutoDownloadsPerProjectPerHour { get; internal set; }
    public int MaximumFramesPerError { get; internal set; }
    public int MaximumProcessingTimeMilliseconds { get; internal set; }
    public int AutoDownloadRefreshIntervalMinutes { get; internal set; }
    public int ParsedSourceMapCacheLifetimeMinutes { get; internal set; }
    public long MaximumParsedSourceMapCacheSize { get; internal set; }

    public TimeSpan RequestTimeout => TimeSpan.FromMilliseconds(RequestTimeoutMilliseconds);
    public TimeSpan MaximumProcessingTime => TimeSpan.FromMilliseconds(MaximumProcessingTimeMilliseconds);
    public TimeSpan AutoDownloadRefreshInterval => TimeSpan.FromMinutes(AutoDownloadRefreshIntervalMinutes);
    public TimeSpan ParsedSourceMapCacheLifetime => TimeSpan.FromMinutes(ParsedSourceMapCacheLifetimeMinutes);

    public static SourceMapOptions ReadFromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("SourceMaps");
        return new SourceMapOptions
        {
            EnableAutoDownload = section.GetValue(nameof(EnableAutoDownload), true),
            RequestTimeoutMilliseconds = ReadPositive(section, nameof(RequestTimeoutMilliseconds), 3000),
            MaximumGeneratedFileSize = ReadPositive(section, nameof(MaximumGeneratedFileSize), 5 * 1024 * 1024),
            MaximumSourceMapSize = ReadPositive(section, nameof(MaximumSourceMapSize), 20 * 1024 * 1024),
            MaximumMappingSegments = ReadPositive(section, nameof(MaximumMappingSegments), 1_000_000),
            MaximumRedirects = Math.Max(0, section.GetValue(nameof(MaximumRedirects), 3)),
            MaximumConcurrentDownloads = ReadPositive(section, nameof(MaximumConcurrentDownloads), 4),
            MaximumAutoDownloadsPerProjectPerHour = Math.Max(0, section.GetValue(nameof(MaximumAutoDownloadsPerProjectPerHour), 100)),
            MaximumFramesPerError = ReadPositive(section, nameof(MaximumFramesPerError), 100),
            MaximumProcessingTimeMilliseconds = ReadPositive(section, nameof(MaximumProcessingTimeMilliseconds), 5000),
            AutoDownloadRefreshIntervalMinutes = ReadPositive(section, nameof(AutoDownloadRefreshIntervalMinutes), 60),
            ParsedSourceMapCacheLifetimeMinutes = ReadPositive(section, nameof(ParsedSourceMapCacheLifetimeMinutes), 5),
            MaximumParsedSourceMapCacheSize = Math.Max(1, section.GetValue(nameof(MaximumParsedSourceMapCacheSize), 100L * 1024 * 1024))
        };
    }

    private static int ReadPositive(IConfiguration section, string name, int defaultValue)
        => Math.Max(1, section.GetValue(name, defaultValue));
}
