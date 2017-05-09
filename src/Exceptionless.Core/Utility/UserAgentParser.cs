using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Foundatio.Caching;
using Foundatio.Logging;
using UAParser;

namespace Exceptionless.Core.Utility {
    public sealed class UserAgentParser {
        private static readonly Lazy<Parser> _parser = new Lazy<Parser>(Parser.GetDefault);
        private readonly InMemoryCacheClient _localCache;
        private readonly ILogger _logger;

        public UserAgentParser(ILoggerFactory loggerFactory) {
            _localCache = new InMemoryCacheClient(new InMemoryCacheClientOptions { LoggerFactory = loggerFactory }) { MaxItems = 250 };
            _logger = loggerFactory.CreateLogger<UserAgentParser>();
        }

        public async Task<ClientInfo> ParseAsync(string userAgent, string projectId = null) {
            if (String.IsNullOrEmpty(userAgent))
                return null;

            var cacheValue = await _localCache.GetAsync<ClientInfo>(userAgent).AnyContext();
            if (cacheValue.HasValue)
                return cacheValue.Value;

            ClientInfo info = null;
            try {
                info = _parser.Value.Parse(userAgent);
            } catch (Exception ex) {
                _logger.Warn().Project(projectId).Message("Unable to parse user agent {0}. Exception: {1}", userAgent, ex.Message).Write();
            }

            await _localCache.SetAsync(userAgent, info).AnyContext();
            return info;
        }
    }
}