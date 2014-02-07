using System;

namespace MongoMigrations {
    public class MigrationException : ApplicationException {
        public MigrationException(string message, Exception innerException)
            : base(message, innerException) {
        }
    }
}