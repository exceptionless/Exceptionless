using System;
using Exceptionless.Core.Repositories;
using MongoMigrations;
using NLog.Fluent;

namespace Exceptionless.Core.Migrations {
    public static class MongoMigrationChecker {
        private static bool _isUpdating;
        public static void ThrowIfNotLatestVersion(string connectionString, string databaseName) {
            var runner = new MigrationRunner(connectionString, databaseName);
            runner.MigrationLocator.LookForMigrationsInAssemblyOfType<EventRepository>();
            runner.DatabaseStatus.ThrowIfNotLatestVersion();
        }

        public static void EnsureLatest(string connectionString, string databaseName) {
            if (_isUpdating)
                return;

            _isUpdating = true;

            try {
                var runner = new MigrationRunner(connectionString, databaseName);
                runner.MigrationLocator.LookForMigrationsInAssemblyOfType<EventRepository>();
                runner.UpdateToLatest();
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error ensuring latest db version: {0}", ex.Message).Write();
            } finally {
                _isUpdating = false;
            }
        }

        public static bool IsUpToDate(string connectionString, string databaseName) {
            try {
                var runner = new MigrationRunner(connectionString, databaseName);
                runner.MigrationLocator.LookForMigrationsInAssemblyOfType<EventRepository>();
                return !runner.DatabaseStatus.IsNotLatestVersion();
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error checking db version: {0}", ex.Message).Write();
            }

            return false;
        }
    }
}