using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace MongoMigrations {
    public class DatabaseMigrationStatus {
        private readonly MigrationRunner _runner;

        public string VersionCollectionName = "_schemaversion";

        public DatabaseMigrationStatus(MigrationRunner runner) {
            _runner = runner;
        }

        public virtual MongoCollection<AppliedMigration> GetMigrationsCollection() {
            return _runner.Database.GetCollection<AppliedMigration>(VersionCollectionName);
        }

        public virtual bool IsNotLatestVersion() {
            return _runner.MigrationLocator.LatestVersion()
                   != GetVersion();
        }

        public virtual void ThrowIfNotLatestVersion() {
            if (!IsNotLatestVersion())
                return;
            
            var databaseVersion = GetVersion();
            var migrationVersion = _runner.MigrationLocator.LatestVersion();

            throw new ApplicationException(String.Format("Database is not the expected version, database is at version: {0}, migrations are at version: {1}", databaseVersion, migrationVersion));
        }

        public virtual MigrationVersion GetVersion() {
            var lastAppliedMigration = GetLastAppliedMigration();
            return lastAppliedMigration == null
                    ? MigrationVersion.Default()
                    : lastAppliedMigration.Version;
        }

        public virtual AppliedMigration GetLastAppliedMigration() {
            return GetMigrationsCollection()
                .Find(Query.NE("CompletedOn", String.Empty))
                .ToList() // in memory but this will never get big enough to matter
                .OrderByDescending(v => v.Version)
                .FirstOrDefault();
        }

        public virtual AppliedMigration StartMigration(Migration migration) {
            var appliedMigration = new AppliedMigration(migration);
            long docCount = 0;
            if (migration is CollectionMigration) {
                var collection = ((CollectionMigration)migration).GetCollection();
                docCount = collection.Count();
            }
            appliedMigration.TotalCount = docCount;
            GetMigrationsCollection().Insert(appliedMigration);
            return appliedMigration;
        }

        public virtual void SetMigrationLastId(MigrationVersion version, string id) {
            GetMigrationsCollection().Update(Query.EQ("_id", new BsonString(version.ToString())), Update.Set("LastCompletedId", id).Inc("CompletedCount", 1));
        }

        public virtual void CompleteMigration(AppliedMigration appliedMigration) {
            GetMigrationsCollection().Update(Query.EQ("_id", new BsonString(appliedMigration.Version.ToString())), Update.Set("CompletedOn", new BsonDateTime(DateTime.Now)));
        }

        public virtual void MarkUpToVersion(MigrationVersion version) {
            _runner.MigrationLocator.GetAllMigrations()
                .Where(m => m.Version <= version)
                .ToList()
                .ForEach(m => MarkVersion(m.Version));
        }

        public virtual void MarkVersion(MigrationVersion version) {
            var appliedMigration = AppliedMigration.MarkerOnly(version);
            GetMigrationsCollection().Insert(appliedMigration);
        }

        public void AddMigrationError(MigrationVersion version, DocumentMigrationError documentMigrationError) {
            var d = new Dictionary<string, object> {
                { "DocumentId", new BsonString(documentMigrationError.DocumentId) },
                { "Error", new BsonString(documentMigrationError.Error) }
            };

            GetMigrationsCollection().Update(Query.EQ("_id", new BsonString(version.ToString())), Update.Push("FailedMigrations", new BsonDocument(d)));
        }
    }
}