using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.Helpers;
using MongoDB.Driver;

namespace Exceptionless.Core.Repositories {
    public class MongoRepositoryManager {
        private readonly MongoDatabase _database;
        private readonly IDependencyResolver _dependencyResolver;
        private static Type[] _repositoryTypes;

        public MongoRepositoryManager(MongoDatabase database, IDependencyResolver dependencyResolver) {
            _dependencyResolver = dependencyResolver;
            _database = database;
        }

        public IEnumerable<IMongoRepositoryManagement> GetRepositories() {
            if (_repositoryTypes == null)
                _repositoryTypes = TypeHelper.GetDerivedTypes<IMongoRepositoryManagement>(new[] { typeof(IMongoRepositoryManagement).Assembly }).ToArray();
            return _repositoryTypes.Select(repositoryType => _dependencyResolver.GetService(repositoryType) as IMongoRepositoryManagement);
        }

        public void InitializeRepositories() {
            foreach (var repository in GetRepositories())
                repository.InitializeCollection(_database);
        }

        public void RemoveAll() {
            foreach (var repository in GetRepositories())
                repository.GetCollection().RemoveAll();
        }

        public IEnumerable<string> GetCollectionNames() {
            return GetRepositories().Select(r => r.GetCollectionName());
        }

        public Type GetCollectionEntityType(string collectionName) {
            var collection = GetRepositories().FirstOrDefault(r => r.GetCollectionName().Equals(collectionName));
            return collection == null ? null : collection.GetDocumentType();
        }
    }
}
