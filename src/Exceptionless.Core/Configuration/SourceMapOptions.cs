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

    public TimeSpan RequestTimeout => TimeSpan.FromMilliseconds(RequestTimeoutMilliseconds);
    public TimeSpan MaximumProcessingTime => TimeSpan.FromMilliseconds(MaximumProcessingTimeMilliseconds);

    public static SourceMapOptions ReadFromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("SourceMaps");
        return new SourceMapOptions
        {
            EnableAutoDownload = section.GetValue(nameof(EnableAutoDownload), true),
            RequestTimeoutMilliseconds = section.GetValue(nameof(RequestTimeoutMilliseconds), 3000),
            MaximumGeneratedFileSize = section.GetValue(nameof(MaximumGeneratedFileSize), 5 * 1024 * 1024),
            MaximumSourceMapSize = section.GetValue(nameof(MaximumSourceMapSize), 20 * 1024 * 1024),
            MaximumMappingSegments = section.GetValue(nameof(MaximumMappingSegments), 1_000_000),
            MaximumRedirects = section.GetValue(nameof(MaximumRedirects), 3),
            MaximumConcurrentDownloads = section.GetValue(nameof(MaximumConcurrentDownloads), 4),
            MaximumAutoDownloadsPerProjectPerHour = section.GetValue(nameof(MaximumAutoDownloadsPerProjectPerHour), 100),
            MaximumFramesPerError = section.GetValue(nameof(MaximumFramesPerError), 100),
            MaximumProcessingTimeMilliseconds = section.GetValue(nameof(MaximumProcessingTimeMilliseconds), 5000)
        };
    }
}
