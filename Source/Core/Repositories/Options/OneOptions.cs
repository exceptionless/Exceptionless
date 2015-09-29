using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Repositories {
    public class OneOptions : QueryOptions {
        public OneOptions() {
            Fields = new List<string>();
        }

        public List<string> Fields { get; set; }
        public string CacheKey { get; set; }
        public TimeSpan? ExpiresIn { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }

        public DateTime GetCacheExpirationDate() {
            if (ExpiresAtUtc.HasValue && ExpiresAtUtc.Value < DateTime.UtcNow)
                throw new ArgumentException("ExpiresAt can't be in the past.");

            if (ExpiresAtUtc.HasValue)
                return ExpiresAtUtc.Value;

            if (ExpiresIn.HasValue)
                return DateTime.UtcNow.Add(ExpiresIn.Value);

            return DateTime.UtcNow.AddSeconds(RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS);
        }

        public bool UseCache => !String.IsNullOrEmpty(CacheKey);
    }
}