using System;
using System.Threading;
using System.Threading.Tasks;

namespace Exceptionless.Core.Geo {
     public interface IGeoIPResolver {
         Task<GeoResult> ResolveIpAsync(string ip, CancellationToken cancellationToken = default(CancellationToken));
    }
}