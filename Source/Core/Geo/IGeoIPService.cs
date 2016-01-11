using System;
using System.Threading;
using System.Threading.Tasks;

namespace Exceptionless.Core.Geo {
    public interface IGeoIPService {
        Task<GeoResult> ResolveIpAsync(string ip, CancellationToken cancellationToken = default(CancellationToken));
    }
}