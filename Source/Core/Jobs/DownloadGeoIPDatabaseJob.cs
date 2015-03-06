using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Utility;
using Foundatio.Jobs;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public class DownloadGeoIPDatabaseJob : JobBase {
        protected override async Task<JobResult> RunInternalAsync(CancellationToken token) {
            var path = PathHelper.ExpandPath(Settings.Current.GeoIPDatabasePath);
            if (String.IsNullOrEmpty(path)) {
                Log.Error().Message("No GeoIPDatabasePath was specified.").Write();
                return JobResult.FailedWithMessage("No GeoIPDatabasePath was specified.");
            }

            try {
                if (File.Exists(path)) {
                    Log.Info().Message("Deleting existing GeoIP database \"{0}\".", path).Write();
                    File.Delete(path);
                }

                Log.Info().Message("Downloading GeoIP database.").Write();
                var client = new HttpClient();
                var file = await client.GetAsync("http://geolite.maxmind.com/download/geoip/database/GeoLite2-City.mmdb.gz", token);
                if (!file.IsSuccessStatusCode)
                    return JobResult.FailedWithMessage("Unable to download GeoIP database.");

                Log.Info().Message("Extracting GeoIP database to \"{0}\".", path).Write();
                using (FileStream decompressedFileStream = new FileStream(path, FileMode.CreateNew)) {
                    using (GZipStream decompressionStream = new GZipStream(await file.Content.ReadAsStreamAsync(), CompressionMode.Decompress)) {
                        decompressionStream.CopyTo(decompressedFileStream);
                    }
                }
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("An error occurred while downloading the GeoIP database \"{0}\".", path).Write();
                return JobResult.FromException(ex);
            }

            Log.Info().Message("Finished downloading GeoIP database.").Write();
            return JobResult.Success;
        }
    }
}