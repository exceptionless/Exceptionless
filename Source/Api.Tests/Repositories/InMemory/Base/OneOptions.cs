using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public class OneOptions<T> : QueryOptions<T> where T : IIdentity {
        public OneOptions() {
            Fields = new List<string>();
        }

        public List<string> Fields { get; set; }
        public Expression SortBy { get; set; }
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