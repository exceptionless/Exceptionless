#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

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
                Log.Error().Exception(ex).Message("Error ensuring latest db version: {0}", ex.Message).Report().Write();
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
                Log.Error().Exception(ex).Message("Error checking db version: {0}", ex.Message).Report().Write();
            }

            return false;
        }
    }
}