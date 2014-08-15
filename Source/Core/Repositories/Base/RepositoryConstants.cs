using System;

namespace Exceptionless.Core.Repositories {
    public static class RepositoryConstants {
        public const int DEFAULT_CACHE_EXPIRATION_SECONDS = 60 * 5;
        public const int DEFAULT_LIMIT = 10;
        public const int MAX_LIMIT = 100;
        public const int BATCH_SIZE = 150;
    }
}