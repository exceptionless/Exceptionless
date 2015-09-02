using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace MongoMigrations {
    public class MigrationRunner {
        public const string DEFAULT_COLLECTION_NAME = "_schemaversion";

        static MigrationRunner() {
            Init();
        }

        public static void Init() {
            BsonSerializer.RegisterSerializer(typeof(MigrationVersion), new MigrationVersionSerializer());
        }

        public MigrationRunner(string connectionString, string databaseName, string collectionName = DEFAULT_COLLECTION_NAME) {
            var client = new MongoClient(connectionString);
            Database = client.GetServer().GetDatabase(databaseName);
            DatabaseStatus = new DatabaseMigrationStatus(this);
            MigrationLocator = new MigrationLocator();
        }

        public MigrationRunner(MongoDatabase database) {
            Database = database;
        }

        public MongoDatabase Database { get; set; }
        public MigrationLocator MigrationLocator { get; set; }
        public DatabaseMigrationStatus DatabaseStatus { get; set; }

        public virtual void UpdateToLatest() {
            Trace.TraceInformation("Updating {0} database to latest...", Database.Name);
            UpdateTo(MigrationLocator.LatestVersion());
        }

        protected virtual void ApplyMigrations(IEnumerable<Migration> migrations) {
            migrations.ToList().ForEach(ApplyMigration);
        }

        protected virtual void ApplyMigration(Migration migration) {
            Trace.TraceInformation("Applying migration \"{0}\" for version {1} to database \"{2}\".", migration.Description, migration.Version, Database.Name);

            migration.Database = Database;
            var appliedMigration = DatabaseStatus.StartMigration(migration);
            try {
                var m = migration as CollectionMigration;
                if (m != null) {
                    m.MigrationErrorCallback = MigrationErrorCallback;
                    m.MigrationProgressCallback = MigrationProgressCallback;
                }
                migration.Update();
            } catch (Exception exception) {
                OnMigrationException(migration, exception);
            }

            DatabaseStatus.CompleteMigration(appliedMigration);
        }

        private void MigrationProgressCallback(Migration migration, string id) {
            DatabaseStatus.SetMigrationLastId(migration.Version, id);
        }

        private void MigrationErrorCallback(Migration migration, DocumentMigrationError documentMigrationError) {
            DatabaseStatus.AddMigrationError(migration.Version, documentMigrationError);
        }

        protected virtual void OnMigrationException(Migration migration, Exception exception) {
            string message = String.Format("Failed applying migration \"{0}\" for version {1} to database \"{2}\": {3}", migration.Description, migration.Version, Database.Name, exception.Message);
            Trace.TraceError(message);
            throw new MigrationException(message, exception);
        }

        public virtual void UpdateTo(MigrationVersion updateToVersion) {
            var currentVersion = DatabaseStatus.GetLastAppliedMigration();
            var migrations = MigrationLocator.GetMigrationsAfter(currentVersion).Where(m => m.Version <= updateToVersion).ToList();
            if (migrations.Count == 0)
                return;

            // if the migration collection didn't exist, assume it's a new db that is already up to date.
            if (currentVersion == null) {
                foreach (var migration in migrations)
                    DatabaseStatus.MarkVersion(migration.Version);
                
                return;
            }

            Trace.TraceInformation("Updating migration \"{0}\" for version {1} to database \"{2}\".", currentVersion, updateToVersion, Database.Name);
            ApplyMigrations(migrations);
        }
    }
}