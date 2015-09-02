using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using MongoDB.Driver;
using NLog.Fluent;
#pragma warning disable 1998

namespace Exceptionless.EventMigration {
    public class MongoCopyJob : JobBase {
        private readonly ICacheClient _cacheClient;
        private readonly MongoDatabase _sourceMongoDatabase;
        private readonly MongoDatabase _mongoDatabase;

        public MongoCopyJob(ICacheClient cacheClient, MongoDatabase mongoDatabase) {
            _cacheClient = cacheClient; 
            _sourceMongoDatabase = GetMongoDatabase();
            _mongoDatabase = mongoDatabase;
        }

        protected override async Task<JobResult> RunInternalAsync(CancellationToken token) {
            var ip = Util.GetExternalIP();
            if (ip != null)
                Log.Info().Message("Public IP:" + ip).Write();

            CopyCollections("error", "errorstack", "errorstack.stats.day", "errorstack.stats.month", "jobhistory", "joblock", "log", "project.stats.day", "project.stats.month");
            
            Log.Info().Message("Running migrations...").Write();
            MongoMigrationChecker.EnsureLatest(Settings.Current.MongoConnectionString, new MongoUrl(Settings.Current.MongoConnectionString).DatabaseName);
            Log.Info().Message("Finished running migrations").Write();

            Log.Info().Message("Creating indexes...").Write();
            new ApplicationRepository(_mongoDatabase);
            new OrganizationRepository(_mongoDatabase);
            new ProjectRepository(_mongoDatabase);
            new TokenRepository(_mongoDatabase);
            new WebHookRepository(_mongoDatabase);
            new UserRepository(_mongoDatabase);
            Log.Info().Message("Finished creating indexes...").Write();

            Log.Info().Message("Flushing redis cache").Write();
            _cacheClient.FlushAll();

            return JobResult.Success;
        }

        private void CopyCollections(params string[] exclusions) {
            foreach (var collectionName in _sourceMongoDatabase.GetCollectionNames()) {
                if (collectionName.StartsWith("system") || exclusions.Contains(exclusion => String.Equals(collectionName, exclusion)))
                    continue;
                
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

                Log.Info().Message("Finished copying collection: {0}", collectionName).Write();
            }
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