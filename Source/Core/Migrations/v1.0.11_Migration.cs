#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public class AddSignatureInfoToErrorMigration : CollectionMigration {
        public AddSignatureInfoToErrorMigration()
            : base("1.0.13", EventRepository.CollectionName) {
            Description = "Add signature info to error.";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            var errorRepository = new EventRepository(collection.Database, null, null, null);
            BsonValue id = document.GetDocumentId();
            if (id == null || !id.IsObjectId)
                return;

            Error error = errorRepository.GetById(id.ToString());
            if (error == null)
                return;

            if (document.Contains("sig"))
                document.Remove("sig");

            var signatureFactory = new ErrorSignatureFactory();
            // updates the document to set the IsSignatureTarget
            ErrorSignature signature = signatureFactory.GetSignature(error);
            errorRepository.Update(error);
        }
    }

    public class ChangeAppPathToPathMigration : CollectionMigration {
        public ChangeAppPathToPathMigration()
            : base("1.0.12", StackRepository.CollectionName) {
            Description = "Change AppPath to Path.";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            if (!document.Contains(StackRepository.FieldNames.SignatureInfo))
                return;

            var signatureInfo = document.GetElement(StackRepository.FieldNames.SignatureInfo).Value.AsBsonDocument;
            bool renamed = signatureInfo.ChangeName("AppPath", "Path");

            if (renamed)
                collection.Save(document);
        }
    }
}