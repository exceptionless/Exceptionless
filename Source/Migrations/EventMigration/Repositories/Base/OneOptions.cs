using System;
using System.Collections.Generic;

namespace Exceptionless.EventMigration.Repositories {
    public class OneOptions : QueryOptions {
        public OneOptions() {
            Fields = new List<string>();
        }

        public List<string> Fields { get; set; }
        public string CacheKey { get; set; }
        public TimeSpan? ExpiresIn { get; set; }
        public DateTime? ExpiresAt { get; set; }

        public DateTime GetCacheExpirationDate() {
            if (ExpiresAt.HasValue && ExpiresAt.Value < DateTime.Now)
                throw new ArgumentException("ExpiresAt can't be in the past.");

            if (ExpiresAt.HasValue)
                return ExpiresAt.Value;

            if (ExpiresIn.HasValue)
                return DateTime.Now.Add(ExpiresIn.Value);

            return DateTime.Now.AddSeconds(RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS);
        }

        public bool UseCache {
            get { return !String.IsNullOrEmpty(CacheKey); }
        }
    }
}