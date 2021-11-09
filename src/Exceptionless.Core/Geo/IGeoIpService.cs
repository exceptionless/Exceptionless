namespace Exceptionless.Core.Geo;

public interface IGeoIpService {
    Task<GeoResult> ResolveIpAsync(string ip, CancellationToken cancellationToken = default);
}
