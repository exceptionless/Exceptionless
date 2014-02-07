#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public class ChangeRequestInfoFormToPostData : CollectionMigration {
        public ChangeRequestInfoFormToPostData()
            : base("1.0.20", ErrorRepository.CollectionName) {
            Description = "Change RequestInfo.Form to RequestInfo.PostData.";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            if (!document.Contains(ErrorRepository.FieldNames.RequestInfo))
                return;

            BsonDocument requestInfo = document.GetElement(ErrorRepository.FieldNames.RequestInfo).Value.AsBsonDocument;
            if (!requestInfo.Contains("frm"))
                return;

            requestInfo.Add(ErrorRepository.FieldNames.PostData, new BsonString(requestInfo.GetElement("frm").Value.ToJson()));
            requestInfo.Remove("frm");

            collection.Save(document);
        }
    }
}