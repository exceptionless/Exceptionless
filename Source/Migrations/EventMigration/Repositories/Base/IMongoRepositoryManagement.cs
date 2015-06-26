using System;
using MongoDB.Driver;

namespace Exceptionless.EventMigration.Repositories {
    public interface IMongoRepositoryManagement {
        void InitializeCollection(MongoDatabase database);
        MongoCollection GetCollection();
        string GetCollectionName();
        Type GetDocumentType();
    }
}