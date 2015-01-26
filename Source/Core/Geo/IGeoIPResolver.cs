using System;

namespace Exceptionless.Core.Geo {
     public interface IGeoIPResolver {
        Location ResolveIp(string ip);
    }
}