#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public class ChangeExtendedDataKeysMigration : CollectionMigration {
        public ChangeExtendedDataKeysMigration()
            : base("1.0.14", ErrorRepository.CollectionName) {
            Description = "Change extended data keys.";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            bool renamed = false;
            BsonDocument currentDoc = document;
            while (currentDoc != null) {
                if (currentDoc.Contains(ErrorRepository.FieldNames.ExtendedData)) {
                    BsonValue extendedData = currentDoc.GetElement(ErrorRepository.FieldNames.ExtendedData).Value;
                    if (extendedData.IsBsonArray) {
                        var newDoc = new BsonDocument(extendedData.AsBsonArray.Where(a => a.AsBsonArray.Count == 2).Select(a => new BsonElement(a.AsBsonArray[0].AsString.Replace('.', '_'), a.AsBsonArray[1])));
                        currentDoc.Set(ErrorRepository.FieldNames.ExtendedData, newDoc);
                        extendedData = newDoc;
                    }

                    renamed |= extendedData.AsBsonDocument.ChangeName("ExtraExceptionProperties", DataDictionary.EXCEPTION_INFO_KEY);
                    renamed |= extendedData.AsBsonDocument.ChangeName("ExceptionInfo", DataDictionary.EXCEPTION_INFO_KEY);
                    renamed |= extendedData.AsBsonDocument.ChangeName("TraceInfo", DataDictionary.TRACE_LOG_KEY);
                }

                if (currentDoc.Contains(ErrorRepository.FieldNames.Inner)) {
                    BsonValue v = currentDoc.GetElement(ErrorRepository.FieldNames.Inner).Value;
                    currentDoc = !v.IsBsonNull ? v.AsBsonDocument : null;
                } else
                    currentDoc = null;
            }

            if (renamed)
                collection.Save(document);
        }
    }
}