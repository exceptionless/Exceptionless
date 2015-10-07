using System;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Storage;

namespace Exceptionless.Core.Jobs {
    public class DownloadGeoIPDatabaseJob : JobBase {
        private readonly IFileStorage _storage;

        public DownloadGeoIPDatabaseJob(IFileStorage storage) {
            _storage = storage;
        }

        protected override async Task<JobResult> RunInternalAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            try {
                if (await _storage.ExistsAsync(MindMaxGeoIPResolver.GEO_IP_DATABASE_PATH).AnyContext()) {
                    Logger.Info().Message("Deleting existing GeoIP database.").Write();
                    await _storage.DeleteFileAsync(MindMaxGeoIPResolver.GEO_IP_DATABASE_PATH, cancellationToken).AnyContext();
                }

                Logger.Info().Message("Downloading GeoIP database.").Write();
                var client = new HttpClient();
                var file = await client.GetAsync("http://geolite.maxmind.com/download/geoip/database/GeoLite2-City.mmdb.gz", cancellationToken).AnyContext();
                if (!file.IsSuccessStatusCode)
                    return JobResult.FailedWithMessage("Unable to download GeoIP database.");

                Logger.Info().Message("Extracting GeoIP database").Write();
                using (GZipStream decompressionStream = new GZipStream(await file.Content.ReadAsStreamAsync().AnyContext(), CompressionMode.Decompress))
                    await _storage.SaveFileAsync(MindMaxGeoIPResolver.GEO_IP_DATABASE_PATH, decompressionStream, cancellationToken).AnyContext();
            } catch (Exception ex) {
                Logger.Error().Exception(ex).Message("An error occurred while downloading the GeoIP database.").Write();
                return JobResult.FromException(ex);
            }

            Logger.Info().Message("Finished downloading GeoIP database.").Write();
            return JobResult.Success;
        }
    }
}