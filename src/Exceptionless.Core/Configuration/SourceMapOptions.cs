using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public sealed class SourceMapOptions
{
    public bool EnableAutoDownload { get; internal set; }
    public int RequestTimeoutMilliseconds { get; internal set; }
    public int MaximumGeneratedFileSize { get; internal set; }
    public int MaximumSourceMapSize { get; internal set; }
    public int MaximumArtifactsPerProject { get; internal set; }
    public long MaximumStorageSizePerProject { get; internal set; }
    public int MaximumArtifactsPerFreeProject { get; internal set; }
    public long MaximumStorageSizePerFreeProject { get; internal set; }
    public int MaximumMappingSegments { get; internal set; }
    public int MaximumRedirects { get; internal set; }
    public int MaximumConcurrentDownloads { get; internal set; }
    public int MaximumConcurrentDownloadsGlobally { get; internal set; }
    public int AutoDownloadRateLimitPeriodMinutes { get; internal set; }
    public int MaximumAutoDiscoveriesPerFreeClientKey { get; internal set; }
    public int MaximumAutoDiscoveriesPerClientKey { get; internal set; }
    public int MaximumAutoDiscoveriesPerFreeProject { get; internal set; }
    public int MaximumAutoDiscoveriesPerProject { get; internal set; }
    public int MaximumAutoDiscoveriesPerFreeOrganization { get; internal set; }
    public int MaximumAutoDiscoveriesPerOrganization { get; internal set; }
    public int MaximumAutoDownloadRequestsPerDestination { get; internal set; }
    public int MaximumAutoDownloadConnectionsPerIpAddress { get; internal set; }
    public int MaximumAutoDownloadRequestsGlobally { get; internal set; }
    public int MaximumAutoRefreshRequestsPerDestination { get; internal set; }
    public int MaximumAutoRefreshRequestsGlobally { get; internal set; }
    public int MaximumFramesPerError { get; internal set; }
    public int MaximumProcessingTimeMilliseconds { get; internal set; }
    public int AutoDownloadRefreshIntervalMinutes { get; internal set; }
    public int ParsedSourceMapCacheLifetimeMinutes { get; internal set; }
    public long MaximumParsedSourceMapCacheSize { get; internal set; }
    public int UsageTrackingDebounceMinutes { get; internal set; }
    public int FreeArtifactRetentionDays { get; internal set; }
    public int ArtifactRetentionDays { get; internal set; }

    public TimeSpan RequestTimeout => TimeSpan.FromMilliseconds(RequestTimeoutMilliseconds);
    public TimeSpan MaximumProcessingTime => TimeSpan.FromMilliseconds(MaximumProcessingTimeMilliseconds);
    public TimeSpan AutoDownloadRefreshInterval => TimeSpan.FromMinutes(AutoDownloadRefreshIntervalMinutes);
    public TimeSpan ParsedSourceMapCacheLifetime => TimeSpan.FromMinutes(ParsedSourceMapCacheLifetimeMinutes);
    public TimeSpan AutoDownloadRateLimitPeriod => TimeSpan.FromMinutes(AutoDownloadRateLimitPeriodMinutes);
    public TimeSpan UsageTrackingDebounce => TimeSpan.FromMinutes(UsageTrackingDebounceMinutes);
    public TimeSpan FreeArtifactRetention => TimeSpan.FromDays(FreeArtifactRetentionDays);
    public TimeSpan ArtifactRetention => TimeSpan.FromDays(ArtifactRetentionDays);

    public static SourceMapOptions ReadFromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("SourceMaps");
        return new SourceMapOptions
        {
            EnableAutoDownload = section.GetValue(nameof(EnableAutoDownload), true),
            RequestTimeoutMilliseconds = ReadPositive(section, nameof(RequestTimeoutMilliseconds), 3000),
            MaximumGeneratedFileSize = ReadPositive(section, nameof(MaximumGeneratedFileSize), 5 * 1024 * 1024),
            MaximumSourceMapSize = ReadPositive(section, nameof(MaximumSourceMapSize), 20 * 1024 * 1024),
            MaximumArtifactsPerProject = ReadPositive(section, nameof(MaximumArtifactsPerProject), 1000),
            MaximumStorageSizePerProject = ReadPositive(section, nameof(MaximumStorageSizePerProject), 1024L * 1024 * 1024),
            MaximumArtifactsPerFreeProject = ReadPositive(section, nameof(MaximumArtifactsPerFreeProject), 100),
            MaximumStorageSizePerFreeProject = ReadPositive(section, nameof(MaximumStorageSizePerFreeProject), 100L * 1024 * 1024),
            MaximumMappingSegments = ReadPositive(section, nameof(MaximumMappingSegments), 1_000_000),
            MaximumRedirects = Math.Max(0, section.GetValue(nameof(MaximumRedirects), 3)),
            MaximumConcurrentDownloads = ReadPositive(section, nameof(MaximumConcurrentDownloads), 4),
            MaximumConcurrentDownloadsGlobally = ReadPositive(section, nameof(MaximumConcurrentDownloadsGlobally), 16),
            AutoDownloadRateLimitPeriodMinutes = ReadPositive(section, nameof(AutoDownloadRateLimitPeriodMinutes), 15),
            MaximumAutoDiscoveriesPerFreeClientKey = Math.Max(0, section.GetValue(nameof(MaximumAutoDiscoveriesPerFreeClientKey), 5)),
            MaximumAutoDiscoveriesPerClientKey = Math.Max(0, section.GetValue(nameof(MaximumAutoDiscoveriesPerClientKey), 25)),
            MaximumAutoDiscoveriesPerFreeProject = Math.Max(0, section.GetValue(nameof(MaximumAutoDiscoveriesPerFreeProject), 10)),
            MaximumAutoDiscoveriesPerProject = Math.Max(0, section.GetValue(nameof(MaximumAutoDiscoveriesPerProject), 50)),
            MaximumAutoDiscoveriesPerFreeOrganization = Math.Max(0, section.GetValue(nameof(MaximumAutoDiscoveriesPerFreeOrganization), 10)),
            MaximumAutoDiscoveriesPerOrganization = Math.Max(0, section.GetValue(nameof(MaximumAutoDiscoveriesPerOrganization), 100)),
            MaximumAutoDownloadRequestsPerDestination = Math.Max(0, section.GetValue(nameof(MaximumAutoDownloadRequestsPerDestination), 100)),
            MaximumAutoDownloadConnectionsPerIpAddress = Math.Max(0, section.GetValue(nameof(MaximumAutoDownloadConnectionsPerIpAddress), 200)),
            MaximumAutoDownloadRequestsGlobally = Math.Max(0, section.GetValue(nameof(MaximumAutoDownloadRequestsGlobally), 1000)),
            MaximumAutoRefreshRequestsPerDestination = Math.Max(0, section.GetValue(nameof(MaximumAutoRefreshRequestsPerDestination), 20)),
            MaximumAutoRefreshRequestsGlobally = Math.Max(0, section.GetValue(nameof(MaximumAutoRefreshRequestsGlobally), 200)),
            MaximumFramesPerError = ReadPositive(section, nameof(MaximumFramesPerError), 100),
            MaximumProcessingTimeMilliseconds = ReadPositive(section, nameof(MaximumProcessingTimeMilliseconds), 5000),
            AutoDownloadRefreshIntervalMinutes = ReadPositive(section, nameof(AutoDownloadRefreshIntervalMinutes), 60),
            ParsedSourceMapCacheLifetimeMinutes = ReadPositive(section, nameof(ParsedSourceMapCacheLifetimeMinutes), 5),
            MaximumParsedSourceMapCacheSize = Math.Max(1, section.GetValue(nameof(MaximumParsedSourceMapCacheSize), 100L * 1024 * 1024)),
            UsageTrackingDebounceMinutes = ReadPositive(section, nameof(UsageTrackingDebounceMinutes), 60),
            FreeArtifactRetentionDays = ReadPositive(section, nameof(FreeArtifactRetentionDays), 14),
            ArtifactRetentionDays = ReadPositive(section, nameof(ArtifactRetentionDays), 90)
        };
    }

    private static int ReadPositive(IConfiguration section, string name, int defaultValue)
        => Math.Max(1, section.GetValue(name, defaultValue));

    private static long ReadPositive(IConfiguration section, string name, long defaultValue)
        => Math.Max(1, section.GetValue(name, defaultValue));
}
