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
using MongoDB.Driver.Builders;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public class UpdateFixedAndHiddenMigration : CollectionMigration {
        public UpdateFixedAndHiddenMigration()
            : base("1.0.28", StackRepository.CollectionName) {
            Description = "Update fixed and hidden flags on error docs.";
        }

        public override void Update() {
            var errorCollection = Database.GetCollection(EventRepository.CollectionName);

            if (errorCollection.IndexExistsByName("pid_-1_dt.0_-1"))
                errorCollection.DropIndexByName("pid_-1_dt.0_-1");

            base.Update();
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            var errorCollection = Database.GetCollection(EventRepository.CollectionName);

            ObjectId stackId = document.GetValue(StackRepository.FieldNames.Id).AsObjectId;
            if (stackId == ObjectId.Empty)
                return;

            BsonValue value;
            bool isHidden = false;
            if (document.TryGetValue(StackRepository.FieldNames.IsHidden, out value))
                isHidden = value.AsBoolean;

            DateTime? dateFixed = null;

            if (document.TryGetValue(StackRepository.FieldNames.DateFixed, out value))
                dateFixed = value.ToNullableUniversalTime();

            IMongoQuery query = Query.EQ(EventRepository.FieldNames.ErrorStackId, new BsonObjectId(stackId));

            var update = new UpdateBuilder();
            if (isHidden)
                update.Set(EventRepository.FieldNames.IsHidden, true);
            if (dateFixed.HasValue)
                update.Set(EventRepository.FieldNames.IsFixed, true);

            if (isHidden || dateFixed.HasValue)
                errorCollection.Update(query, update, UpdateFlags.Multi);
        }
    }
}