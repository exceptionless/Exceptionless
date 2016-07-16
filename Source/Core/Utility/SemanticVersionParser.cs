using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Foundatio.Caching;
using Foundatio.Logging;
using McSherry.SemanticVersioning;

namespace Exceptionless.Core.Utility {
    public class SemanticVersionParser {
        private readonly InMemoryCacheClient _localCache = new InMemoryCacheClient { MaxItems = 250 };
        private readonly ILogger _logger;

        public SemanticVersionParser(ILoggerFactory loggerFactory) {
            _logger = loggerFactory.CreateLogger(GetType());
        }

        public SemanticVersion Default { get; } = new SemanticVersion(0, 0);

        public async Task<SemanticVersion> ParseAsync(string version) {
            version = version?.Trim();
            if (String.IsNullOrEmpty(version))
                return null;
            
            var cacheValue = await _localCache.GetAsync<SemanticVersion>(version).AnyContext();
            if (cacheValue.HasValue)
                return cacheValue.Value;
            
            int spaceIndex = version.IndexOf(" ", StringComparison.OrdinalIgnoreCase);
            if (spaceIndex > 0)
                version = version.Substring(0, spaceIndex).Trim();
            
            SemanticVersion semanticVersion = null;
            if (version.Length >= 5 && SemanticVersion.TryParse(version, out semanticVersion)) {
                await _localCache.SetAsync(version, semanticVersion).AnyContext();
                return semanticVersion;
            }
            
            Version v;
            int major;
            if (version.Length >= 3 && Version.TryParse(version, out v))
                semanticVersion = new SemanticVersion(v.Major > 0 ? v.Major : 0, v.Minor > 0 ? v.Minor : 0, v.Build > 0 ? v.Build : 0, v.Revision >= 0 ? new[] { v.Revision.ToString() } : Enumerable.Empty<string>());
            else if (Int32.TryParse(version, out major))
                semanticVersion = new SemanticVersion(major, 0);
            else
                _logger.Info("Unable to parse version: {version}", version);

            await _localCache.SetAsync(version, semanticVersion).AnyContext();
            return semanticVersion;
        }
    }
}
