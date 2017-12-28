using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Foundatio.Caching;
using McSherry.SemanticVersioning;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Utility {
    public class SemanticVersionParser {
        private static readonly IReadOnlyCollection<string> EmptyIdentifiers = new List<string>(0).AsReadOnly();
        private readonly InMemoryCacheClient _localCache;
        private readonly ILogger _logger;

        public SemanticVersionParser(ILoggerFactory loggerFactory) {
            _localCache = new InMemoryCacheClient(new InMemoryCacheClientOptions { LoggerFactory = loggerFactory }) { MaxItems = 250 };
            _logger = loggerFactory.CreateLogger<SemanticVersionParser>();
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

            int wildCardIndex = version.IndexOf("*", StringComparison.OrdinalIgnoreCase);
            if (wildCardIndex > 0)
                version = version.Replace(".*", String.Empty).Replace("*", String.Empty);

            SemanticVersion semanticVersion = null;
            if (version.Length >= 5 && SemanticVersion.TryParse(version, out semanticVersion)) {
                await _localCache.SetAsync(version, semanticVersion).AnyContext();
                return semanticVersion;
            }

            if (version.Length >= 3 && Version.TryParse(version, out var v))
                semanticVersion = new SemanticVersion(v.Major > 0 ? v.Major : 0, v.Minor > 0 ? v.Minor : 0, v.Build > 0 ? v.Build : 0, v.Revision >= 0 ? new[] { v.Revision.ToString() } : EmptyIdentifiers);
            else if (Int32.TryParse(version, out var major))
                semanticVersion = new SemanticVersion(major, 0);
            else
                _logger.LogInformation("Unable to parse version: {Version}", version);

            await _localCache.SetAsync(version, semanticVersion).AnyContext();
            return semanticVersion;
        }
    }
}
