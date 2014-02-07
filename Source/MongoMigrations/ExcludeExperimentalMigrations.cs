using System.Linq;

namespace MongoMigrations {
    public class ExcludeExperimentalMigrations : MigrationFilter {
        public override bool Exclude(Migration migration) {
            if (migration == null)
                return false;

            return migration.GetType()
                .GetCustomAttributes(true)
                .OfType<ExperimentalAttribute>()
                .Any();
        }
    }
}