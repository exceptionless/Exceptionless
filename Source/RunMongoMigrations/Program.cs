using System;
using System.Reflection;
using MongoMigrations;

namespace RunMongoMigrations {
    public class Program {
        public static int Main(string[] args) {
            if (args.Length < 3) {
                Console.WriteLine("Usage: RunMongoMigrations server[:port] databaseName migrationAssembly");
                return 1;
            }

            string server = args[0];
            string database = args[1];
            string migrationsAssembly = args[2];

            var runner = new MigrationRunner(("mongodb://" + server), database);

            runner.MigrationLocator.LookForMigrationsInAssembly(Assembly.LoadFrom(migrationsAssembly));

            try {
                runner.UpdateToLatest();
                return 0;
            } catch (MigrationException e) {
                Console.WriteLine("Migrations Failed: " + e);
                Console.WriteLine(args[0], args[1], args[2]);
                return 1;
            }
        }
    }
}