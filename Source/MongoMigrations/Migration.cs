using System;
using MongoDB.Driver;

namespace MongoMigrations {
    public abstract class Migration {
        public MigrationVersion Version { get; protected set; }
        public string Description { get; protected set; }

        protected Migration(MigrationVersion version) {
            Version = version;
        }

        public bool IsSafeToRunMultipleTimes { get; set; }

        public MongoDatabase Database { get; set; }

        public abstract void Update();
    }
}