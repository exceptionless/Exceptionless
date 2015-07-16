using System;
using Exceptionless.Core;

namespace Exceptionless.EventMigration {
    internal class MigrationSettings : SettingsBase<MigrationSettings> {
        public int MigrationBatchSize { get; private set; }

        public bool MigrationCanResetData { get; private set; }

        public string MigrationMongoConnectionString { get; private set; }

        public bool MigrationCanResume { get; private set; }

        public override void Initialize() {
            EnvironmentVariablePrefix = "Exceptionless_";

            MigrationBatchSize = GetInt("Migration:BatchSize", 50);
            MigrationCanResetData = GetBool("Migration:CanResetData");
            MigrationCanResume = GetBool("Migration:Resume", true);
            MigrationMongoConnectionString = GetConnectionString("Migration:MongoConnectionString");
        }
    }
}