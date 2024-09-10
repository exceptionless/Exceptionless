using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Jobs;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Storage;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Insulation.Geo;

public class MaxMindGeoIpService : IGeoIpService, IDisposable
{
    private readonly InMemoryCacheClient _localCache;
    private readonly IFileStorage _storage;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private DatabaseReader? _database;
    private DateTime? _databaseLastChecked;

    public MaxMindGeoIpService(IFileStorage storage, TimeProvider timeProvider, ILoggerFactory loggerFactory)
    {
        _storage = storage;
        _timeProvider = timeProvider;
        _localCache = new InMemoryCacheClient(new InMemoryCacheClientOptions { MaxItems = 250, CloneValues = true, TimeProvider = timeProvider, LoggerFactory = loggerFactory });
        _logger = loggerFactory.CreateLogger<MaxMindGeoIpService>();
    }

    public async Task<GeoResult?> ResolveIpAsync(string ip, CancellationToken cancellationToken = new())
    {
        if (String.IsNullOrEmpty(ip) || (!ip.Contains('.') && !ip.Contains(':')))
            return null;

        ip = ip.Trim();
        if (ip.IsPrivateNetwork())
            return null;

        var cacheValue = await _localCache.GetAsync<GeoResult?>(ip);
        if (cacheValue.HasValue)
            return cacheValue.Value;

        GeoResult? result = null;
        var database = await GetDatabaseAsync(cancellationToken);
        if (database is null)
            return null;

        try
        {
            if (database.TryCity(ip, out var city) && city?.Location is not null)
            {
                result = new GeoResult
                {
                    Latitude = city.Location.Latitude,
                    Longitude = city.Location.Longitude,
                    Country = city.Country.IsoCode,
                    Level1 = city.MostSpecificSubdivision.IsoCode,
                    Locality = city.City.Name
                };
            }

            await _localCache.SetAsync(ip, result);
            return result;
        }
        catch (Exception ex)
        {
            if (ex is GeoIP2Exception)
            {
                _logger.LogTrace(ex, ex.Message);
                await _localCache.SetAsync<GeoResult?>(ip, null);
            }
            else
            {
                _logger.LogError(ex, "Unable to resolve geo location for ip: {IP}", ip);
            }

            return null;
        }
    }

    private async Task<DatabaseReader?> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        // Try to load the new database from disk if the current one is a day old.
        if (_database is not null && _databaseLastChecked.HasValue && _databaseLastChecked.Value < _timeProvider.GetUtcNow().UtcDateTime.SubtractDays(1))
        {
            _database.Dispose();
            _database = null;
        }

        if (_database is not null)
            return _database;

        if (_databaseLastChecked.HasValue && _databaseLastChecked.Value >= _timeProvider.GetUtcNow().UtcDateTime.SubtractSeconds(30))
            return null;

        _databaseLastChecked = _timeProvider.GetUtcNow().UtcDateTime;

        if (!await _storage.ExistsAsync(DownloadGeoIPDatabaseJob.GEO_IP_DATABASE_PATH))
        {
            _logger.LogWarning("No GeoIP database was found");
            return null;
        }

        _logger.LogInformation("Loading GeoIP database");
        try
        {
            using (var stream = await _storage.GetFileStreamAsync(DownloadGeoIPDatabaseJob.GEO_IP_DATABASE_PATH, StreamMode.Read, cancellationToken))
                _database = new DatabaseReader(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to open GeoIP database");
        }

        return _database;
    }

    public void Dispose()
    {
        if (_database is null)
            return;

        _database.Dispose();
        _database = null;

        GC.SuppressFinalize(this);
    }
}
