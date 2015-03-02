namespace MongoMigrations {
    using System;
    using System.Linq;
    using MongoDB.Bson;

    public static class BsonDocumentExtensions {
        /// <summary>
        /// 	Rename all instances of a name in a bson document to the new name.
        /// </summary>
        public static bool ChangeName(this BsonDocument bsonDocument, string originalName, string newName) {
            var elements = bsonDocument.Elements
                .Where(e => e.Name == originalName)
                .ToList();

            if (elements.Count == 0)
                return false;

            foreach (var element in elements) {
                bsonDocument.RemoveElement(element);
                bsonDocument.Add(new BsonElement(newName, element.Value));
            }

            return true;
        }

        public static BsonValue GetDocumentId(this BsonDocument bsonDocument) {
            BsonValue id;
            bsonDocument.TryGetValue("_id", out id);
            return id;
        }
    }
}