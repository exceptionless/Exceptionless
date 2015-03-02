namespace MongoMigrations {
    public abstract class MigrationFilter {
        public abstract bool Exclude(Migration migration);
    }
}