using System;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Storage;
using Foundatio.Utility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs {
    [Job(Description = "Downloads Geo IP database.", IsContinuous = false, Interval = "1d")]
    public class DownloadGeoIPDatabaseJob : JobWithLockBase, IHealthCheck {
        public const string GEO_IP_DATABASE_PATH = "GeoLite2-City.mmdb";
        private readonly AppOptions _options;
        private readonly IFileStorage _storage;
        private readonly ILockProvider _lockProvider;
        private DateTime? _lastRun;

        public DownloadGeoIPDatabaseJob(AppOptions options, ICacheClient cacheClient, IFileStorage storage, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _options = options;
            _storage = storage;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromDays(1));
        }

        protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default) {
            return _lockProvider.AcquireAsync(nameof(DownloadGeoIPDatabaseJob), TimeSpan.FromHours(2), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            _lastRun = SystemClock.UtcNow;

            string licenseKey = _options.MaxMindGeoIpKey;
            if (String.IsNullOrEmpty(licenseKey)) {
                _logger.LogInformation("Configure {SettingKey} to download GeoIP database.", nameof(AppOptions.MaxMindGeoIpKey));
                return JobResult.Success;
            }
            
            try {
                var fi = await _storage.GetFileInfoAsync(GEO_IP_DATABASE_PATH).AnyContext();
                if (fi != null && fi.Modified.IsAfter(SystemClock.UtcNow.StartOfDay())) {
                    _logger.LogInformation("The GeoIP database is already up-to-date.");
                    return JobResult.Success;
                }

                _logger.LogInformation("Downloading GeoIP database.");
                var client = new HttpClient();
                string url = $"https://download.maxmind.com/app/geoip_download?edition_id=GeoLite2-City&license_key={licenseKey}&suffix=tar.gz";
                var file = await client.GetAsync(url, context.CancellationToken).AnyContext();
                if (!file.IsSuccessStatusCode)
                    return JobResult.FailedWithMessage("Unable to download GeoIP database.");

                _logger.LogInformation("Extracting GeoIP database");
                using (var decompressionStream = new GZipStream(await file.Content.ReadAsStreamAsync().AnyContext(), CompressionMode.Decompress))
                    await _storage.SaveFileAsync(GEO_IP_DATABASE_PATH, decompressionStream, context.CancellationToken).AnyContext();
            } catch (Exception ex) {
                _logger.LogError(ex, "An error occurred while downloading the GeoIP database.");
                return JobResult.FromException(ex);
            }

            _logger.LogInformation("Finished downloading GeoIP database.");
            return JobResult.Success;
        }
        
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            if (!_lastRun.HasValue)
                return Task.FromResult(HealthCheckResult.Healthy("Job has not been run yet."));

            if (SystemClock.UtcNow.Subtract(_lastRun.Value) > TimeSpan.FromHours(25))
                return Task.FromResult(HealthCheckResult.Unhealthy("Job has not run in the last 25 hours."));

            return Task.FromResult(HealthCheckResult.Healthy("Job has run in the last 25 hours."));
        }
    }
}