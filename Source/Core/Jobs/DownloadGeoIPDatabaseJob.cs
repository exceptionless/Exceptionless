using System;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Logging;
using Foundatio.Storage;

namespace Exceptionless.Core.Jobs {
    public class DownloadGeoIPDatabaseJob : JobBase {
        private readonly IFileStorage _storage;
        private readonly ILockProvider _lockProvider;

        public DownloadGeoIPDatabaseJob(ICacheClient cacheClient, IFileStorage storage) {
            _storage = storage;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromDays(1));
        }

        protected override Task<ILock> GetJobLockAsync() {
            return _lockProvider.AcquireAsync(nameof(DownloadGeoIPDatabaseJob), TimeSpan.FromHours(2), new CancellationToken(true));
        }
        
        protected override async Task<JobResult> RunInternalAsync(JobRunContext context) {
            try {
                if (await _storage.ExistsAsync(MaxMindGeoIpService.GEO_IP_DATABASE_PATH).AnyContext()) {
                    Logger.Info().Message("Deleting existing GeoIP database.").Write();
                    await _storage.DeleteFileAsync(MaxMindGeoIpService.GEO_IP_DATABASE_PATH, context.CancellationToken).AnyContext();
                }

                Logger.Info().Message("Downloading GeoIP database.").Write();
                var client = new HttpClient();
                var file = await client.GetAsync("http://geolite.maxmind.com/download/geoip/database/GeoLite2-City.mmdb.gz", context.CancellationToken).AnyContext();
                if (!file.IsSuccessStatusCode)
                    return JobResult.FailedWithMessage("Unable to download GeoIP database.");

                Logger.Info().Message("Extracting GeoIP database").Write();
                using (GZipStream decompressionStream = new GZipStream(await file.Content.ReadAsStreamAsync().AnyContext(), CompressionMode.Decompress))
                    await _storage.SaveFileAsync(MaxMindGeoIpService.GEO_IP_DATABASE_PATH, decompressionStream, context.CancellationToken).AnyContext();
            } catch (Exception ex) {
                Logger.Error().Exception(ex).Message("An error occurred while downloading the GeoIP database.").Write();
                return JobResult.FromException(ex);
            }

            Logger.Info().Message("Finished downloading GeoIP database.").Write();
            return JobResult.Success;
        }
    }
}