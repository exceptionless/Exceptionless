#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using CodeSmith.Core.Component;
using Exceptionless.Core.EventPlugins;
using MongoDB.Bson;
using MongoDB.Driver.Builders;
using NLog.Fluent;

namespace Exceptionless.Core.Pipeline {
    [Priority(30)]
    public class CheckForRegressionAction : EventPipelineActionBase {
        private readonly StackRepository _stackRepository;
        private readonly EventRepository _eventRepository;

        public CheckForRegressionAction(StackRepository stackRepository, EventRepository eventRepository) {
            _stackRepository = stackRepository;
            _eventRepository = eventRepository;
        }

        protected override bool ContinueOnError { get { return true; } }

        public override void Process(EventContext ctx) {
            if (ctx.StackInfo == null || !ctx.StackInfo.DateFixed.HasValue || ctx.StackInfo.DateFixed.Value >= ctx.Event.Date.UtcDateTime)
                return;

            Log.Trace().Message("Marking error as an regression.").Write();
            _stackRepository.Collection.Update(
                Query.EQ(StackRepository.FieldNames.Id, new BsonObjectId(new ObjectId(ctx.StackInfo.Id))),
                Update
                    .Unset(StackRepository.FieldNames.DateFixed)
                    .Set(StackRepository.FieldNames.IsRegressed, true));

            _eventRepository.Collection.Update(
                Query.EQ(EventRepository.FieldNames.StackId, new BsonObjectId(new ObjectId(ctx.StackInfo.Id))),
                Update
                    .Unset(EventRepository.FieldNames.IsFixed));

            string signatureHash = ctx.GetProperty<string>("SignatureHash");
            _stackRepository.InvalidateCache(ctx.Event.StackId, signatureHash, ctx.Event.ProjectId);

            ctx.Event.IsFixed = false;
            ctx.IsRegression = true;
        }
    }
}