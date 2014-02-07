using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MongoMigrations {
    public class MigrationLocator {
        protected readonly List<Assembly> Assemblies = new List<Assembly>();
        public readonly List<MigrationFilter> MigrationFilters = new List<MigrationFilter>();

        public MigrationLocator() {
            MigrationFilters.Add(new ExcludeExperimentalMigrations());
        }

        public virtual void LookForMigrationsInAssemblyOfType<T>() {
            var assembly = typeof(T).Assembly;
            LookForMigrationsInAssembly(assembly);
        }

        public void LookForMigrationsInAssembly(Assembly assembly) {
            if (Assemblies.Contains(assembly))
                return;
            
            Assemblies.Add(assembly);
        }

        public virtual IEnumerable<Migration> GetAllMigrations() {
            return Assemblies
                .SelectMany(GetMigrationsFromAssembly)
                .OrderBy(m => m.Version);
        }

        protected virtual IEnumerable<Migration> GetMigrationsFromAssembly(Assembly assembly) {
            try {
                return assembly.GetTypes()
                    .Where(t => typeof(Migration).IsAssignableFrom(t))
                    .Select(Activator.CreateInstance)
                    .OfType<Migration>()
                    .Where(m => !MigrationFilters.Any(f => f.Exclude(m)));
            } catch (Exception exception) {
                throw new MigrationException("Cannot load migrations from assembly: " + assembly.FullName, exception);
            }
        }

        public virtual MigrationVersion LatestVersion() {
            if (!GetAllMigrations().Any())
                return MigrationVersion.Default();
            
            return GetAllMigrations().Max(m => m.Version);
        }

        public virtual IEnumerable<Migration> GetMigrationsAfter(AppliedMigration currentVersion) {
            var migrations = GetAllMigrations();

            if (currentVersion != null)
                migrations = migrations.Where(m => m.Version > currentVersion.Version);

            return migrations.OrderBy(m => m.Version);
        }
    }
}