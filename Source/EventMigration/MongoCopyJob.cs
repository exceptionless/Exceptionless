using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Jobs;
using MongoDB.Driver;
using NLog.Fluent;

namespace Exceptionless.EventMigration {
    public class MongoCopyJob : JobBase {
        private readonly MongoDatabase _sourceMongoDatabase;
        private readonly MongoDatabase _mongoDatabase;

        public MongoCopyJob(MongoDatabase mongoDatabase) {
            _sourceMongoDatabase = GetMongoDatabase();
            _mongoDatabase = mongoDatabase;
        }

        protected override async Task<JobResult> RunInternalAsync(CancellationToken token) {
            var collectionsToCopy = new List<string> { "_schemaversion", "newsletter", "organization", "project", "project.hook", "user" };
            collectionsToCopy.ForEach(CopyCollection);

            return JobResult.Success;
        }

        private void CopyCollection(string collectionName) {
            if (!_sourceMongoDatabase.CollectionExists(collectionName)) {
                Log.Warn().Message("Collection \"{0}\" was not found in the source database.", collectionName).Write();
                return;
            }

            Log.Info().Message("Copying collection: {0}", collectionName).Write();

            if (_mongoDatabase.CollectionExists(collectionName))
                _mongoDatabase.DropCollection(collectionName);

            var source = _sourceMongoDatabase.GetCollection(collectionName);
            var target = _mongoDatabase.GetCollection(collectionName);
            target.InsertBatch(source.FindAll());
        }

        private MongoDatabase GetMongoDatabase() {
            var connectionString = ConfigurationManager.ConnectionStrings["Migration:MongoConnectionString"];
            if (connectionString == null)
                throw new ConfigurationErrorsException("Migration:MongoConnectionString was not found in the app.config.");

            if (String.IsNullOrEmpty(connectionString.ConnectionString))
                throw new ConfigurationErrorsException("Migration:MongoConnectionString was not found in the app.config.");

            MongoDefaults.MaxConnectionIdleTime = TimeSpan.FromMinutes(1);
            var url = new MongoUrl(connectionString.ConnectionString);

            MongoServer server = new MongoClient(url).GetServer();
            return server.GetDatabase(url.DatabaseName);
        }
    }
}