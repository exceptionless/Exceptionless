using System;
using System.Collections.Generic;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using Exceptionless.Tests.Utility;
using MongoDB.Driver;
using Nest;

namespace Exceptionless.Api.Tests.Controllers {
    public class MongoTestHelper {
        private readonly MongoDatabase _database = IoC.GetInstance<MongoDatabase>();
        private readonly MongoRepositoryManager _manager = IoC.GetInstance<MongoRepositoryManager>();
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();

        private static bool _databaseReset = false;
        public void ResetDatabase(bool force = false) {
            if (_databaseReset && !force)
                return;

            _database.Drop();
            _manager.InitializeRepositories();
            _sampleProjectsAdded = false;
            _sampleOrganizationsAdded = false;

            _databaseReset = true;
        }

        public void RemoveAll(string collectionName) {
            var collection = _database.GetCollection(collectionName);
            if (collection != null)
                collection.RemoveAll();

            if (String.Equals(ProjectRepository.CollectionName, collectionName))
                _sampleProjectsAdded = false;

            if (String.Equals(OrganizationRepository.CollectionName, collectionName))
                _sampleOrganizationsAdded = false;
        }

        public long Count(string collectionName) {
            var collection = _database.GetCollection(collectionName);
            return collection != null ? collection.Count() : 0;
        }

        public long ProjectCount() {
            return Count(ProjectRepository.CollectionName);
        }

        public void RemoveAllOrganizations() {
            RemoveAll(OrganizationRepository.CollectionName);
        }

        public void RemoveAllProjects() {
            RemoveAll(ProjectRepository.CollectionName);
        }

        public void RemoveAllEvents() {
            _client.DeleteByQuery<PersistentEvent>(c => c.Type(typeof(PersistentEvent)).Query(q => q.MatchAll()));
        }

        public long EventCount() {
            _client.Refresh(r => r.Force(false));
            return _client.Count<PersistentEvent>(c => c.Query(q => q.MatchAll())).Count;
        }

        public void AddData(string collectionName, IEnumerable<object> entities) {
            var collection = _database.GetCollection(collectionName);
            Type entityType = _manager.GetCollectionEntityType(collectionName);
            if (collection == null)
                return;

            collection.InsertBatch(entityType, entities);
        }

        private static bool _sampleProjectsAdded = false;
        public void AddSampleProjects() {
            if (_sampleProjectsAdded)
                return;

            AddData(ProjectRepository.CollectionName, ProjectData.GenerateSampleProjects());
            _sampleProjectsAdded = true;
        }

        private static bool _sampleOrganizationsAdded = false;
        public void AddSampleOrganizations() {
            if (_sampleOrganizationsAdded)
                return;

            AddData(OrganizationRepository.CollectionName, OrganizationData.GenerateSampleOrganizations());
            _sampleOrganizationsAdded = true;
        }

        public void AddSamples() {
            AddSampleProjects();
            AddSampleOrganizations();
        }
    }
}
