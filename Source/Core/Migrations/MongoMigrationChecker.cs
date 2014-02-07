#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public static class MongoMigrationChecker {
        public static void ThrowIfNotLatestVersion(string connectionString, string databaseName) {
            var runner = new MigrationRunner(connectionString, databaseName);
            runner.MigrationLocator.LookForMigrationsInAssemblyOfType<ErrorRepository>();
            runner.DatabaseStatus.ThrowIfNotLatestVersion();
        }

        public static void EnsureLatest(string connectionString, string databaseName) {
            var runner = new MigrationRunner(connectionString, databaseName);
            runner.MigrationLocator.LookForMigrationsInAssemblyOfType<ErrorRepository>();
            runner.UpdateToLatest();
        }

        public static bool IsUpToDate(string connectionString, string databaseName) {
            var runner = new MigrationRunner(connectionString, databaseName);
            runner.MigrationLocator.LookForMigrationsInAssemblyOfType<ErrorRepository>();
            return !runner.DatabaseStatus.IsNotLatestVersion();
        }
    }
}