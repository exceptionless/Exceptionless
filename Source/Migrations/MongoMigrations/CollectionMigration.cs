using System.Diagnostics;
using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MongoMigrations {
    public abstract class CollectionMigration : Migration {
        protected string CollectionName { get; set; }

        protected CollectionMigration(MigrationVersion version, string collectionName)
            : base(version) {
            CollectionName = collectionName;
        }

        public virtual IMongoQuery Filter() {
            return null;
        }

        public override void Update() {
            var collection = GetCollection();
            var documents = GetDocuments(collection);
            UpdateDocuments(collection, documents);
        }

        public virtual void UpdateDocuments(MongoCollection<BsonDocument> collection, IEnumerable<BsonDocument> documents) {
            foreach (var document in documents) {
                try {
                    UpdateDocument(collection, document);
                    
                    if (MigrationProgressCallback != null)
                        MigrationProgressCallback(this, document.GetDocumentId().ToString());
                } catch (Exception exception) {
                    OnErrorUpdatingDocument(document, exception);
                }
            }
        }

        internal Action<Migration, DocumentMigrationError> MigrationErrorCallback { get; set; }
        internal Action<Migration, string> MigrationProgressCallback { get; set; }

        protected virtual void OnErrorUpdatingDocument(BsonDocument document, Exception exception) {
            var error = new DocumentMigrationError(document.GetDocumentId().ToString(), exception.ToString());
            if (MigrationErrorCallback != null)
                MigrationErrorCallback(this, error);

            string message = String.Format("Failed updating document \"{0}\" in \"{1}\" for migration \"{2}\" for version {3} to database \"{4}\": {5}",
                document.GetDocumentId(), CollectionName, Description, Version, Database.Name, exception.Message);
            Trace.TraceError(message);
        }

        public abstract void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document);

        public virtual MongoCollection<BsonDocument> GetCollection() {
            return Database.GetCollection(CollectionName);
        }

        protected virtual IEnumerable<BsonDocument> GetDocuments(MongoCollection<BsonDocument> collection) {
            var query = Filter();
            return query != null
                    ? collection.Find(query)
                    : collection.FindAll();
        }
    }
}