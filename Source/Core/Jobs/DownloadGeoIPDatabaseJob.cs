using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Geo;
using Foundatio.Jobs;
using Foundatio.Storage;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public class DownloadGeoIPDatabaseJob : JobBase {
        private readonly IFileStorage _storage;

        public DownloadGeoIPDatabaseJob(IFileStorage storage) {
            _storage = storage;
        }

        protected override async Task<JobResult> RunInternalAsync(CancellationToken token) {
            try {
                if (await _storage.ExistsAsync(MindMaxGeoIPResolver.GEO_IP_DATABASE_PATH)) {
                    Log.Info().Message("Deleting existing GeoIP database.").Write();
                    await _storage.DeleteFileAsync(MindMaxGeoIPResolver.GEO_IP_DATABASE_PATH, token);
                }

                Log.Info().Message("Downloading GeoIP database.").Write();
                var client = new HttpClient();
                var file = await client.GetAsync("http://geolite.maxmind.com/download/geoip/database/GeoLite2-City.mmdb.gz", token);
                if (!file.IsSuccessStatusCode)
                    return JobResult.FailedWithMessage("Unable to download GeoIP database.");

                Log.Info().Message("Extracting GeoIP database").Write();
                using (var decompressedMemoryStream = new MemoryStream()) {
                    using (GZipStream decompressionStream = new GZipStream(await file.Content.ReadAsStreamAsync(), CompressionMode.Decompress)) {
                        decompressionStream.CopyTo(decompressedMemoryStream);
                    }

                    await _storage.SaveFileAsync(MindMaxGeoIPResolver.GEO_IP_DATABASE_PATH, decompressedMemoryStream, token);
                }
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("An error occurred while downloading the GeoIP database.").Write();
                return JobResult.FromException(ex);
            }

            Log.Info().Message("Finished downloading GeoIP database.").Write();
            return JobResult.Success;
        }
    }
}