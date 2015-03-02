using System.Collections.Generic;
using System;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoMigrations {
    public class AppliedMigration {
        public const string ManuallyMarked = "Manually marked";

        public AppliedMigration() {
            FailedMigrations = new List<DocumentMigrationError>();
        }

        public AppliedMigration(Migration migration) {
            Version = migration.Version;
            StartedOn = DateTime.Now;
            Description = migration.Description;
            FailedMigrations = new List<DocumentMigrationError>();
        }

        [BsonId]
        public MigrationVersion Version { get; set; }
        public string Description { get; set; }
        public DateTime StartedOn { get; set; }
        public DateTime? CompletedOn { get; set; }
        public long CompletedCount { get; set; }
        public long TotalCount { get; set; }
        public string LastCompletedId { get; set; }
        public List<DocumentMigrationError> FailedMigrations { get; set; }
        public bool ShouldRunAgain { get; set; }

        public override string ToString() {
            return Version.ToString() + " started on " + StartedOn + " completed on " + CompletedOn;
        }

        public static AppliedMigration MarkerOnly(MigrationVersion version) {
            return new AppliedMigration {
                Version = version,
                Description = ManuallyMarked,
                StartedOn = DateTime.Now,
                CompletedOn = DateTime.Now
            };
        }
    }

    public class DocumentMigrationError {
        public DocumentMigrationError(string id, string error) {
            DocumentId = id;
            Error = error;
        }

        public string DocumentId { get; set; }
        public string Error { get; set; }
    }
}