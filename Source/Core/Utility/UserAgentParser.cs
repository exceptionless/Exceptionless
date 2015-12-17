using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Foundatio.Caching;
using Foundatio.Logging;
using UAParser;

namespace Exceptionless.Core.Utility {
    public sealed class UserAgentParser {
        private static readonly Lazy<Parser> _parser = new Lazy<Parser>(Parser.GetDefault);
        private readonly InMemoryCacheClient _cache = new InMemoryCacheClient { MaxItems = 250 };

        public async Task<ClientInfo> ParseAsync(string userAgent, string projectId = null) {
            if (String.IsNullOrEmpty(userAgent))
                return null;

            var cacheValue = await _cache.GetAsync<ClientInfo>(userAgent).AnyContext();
            if (cacheValue.HasValue)
                return cacheValue.Value;

            ClientInfo info = null;
            try {
                info = _parser.Value.Parse(userAgent);
            } catch (Exception ex) {
                Logger.Warn().Project(projectId).Message($"Unable to parse user agent {userAgent}. Exception: {ex.Message}").Write();
            }

            await _cache.SetAsync(userAgent, info).AnyContext();
            return info;
        }
    }
}