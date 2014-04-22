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
using CodeSmith.Core.Component;
using Exceptionless.Core.Plugins.EventPipeline;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Pipeline {
    [Priority(50)]
    public class EnforceRetentionLimitsAction : EventPipelineActionBase {
        private readonly EventRepository _eventRepository;

        public EnforceRetentionLimitsAction(EventRepository eventRepository) {
            _eventRepository = eventRepository;
        }

        protected override bool ContinueOnError { get { return true; } }

        public override void Process(EventContext ctx) {
            if (ctx.IsNew)
                return;

            int maxErrorsPerStack = 50;
            maxErrorsPerStack = ctx.Organization.MaxErrorsPerDay > 0 ? ctx.Organization.MaxErrorsPerDay + Math.Min(50, ctx.Organization.MaxErrorsPerDay * 2) : Int32.MaxValue;

            // Get a list of oldest ids that exceed our desired max errors.
            var errors = _eventRepository.Collection.Find(
                Query.EQ(EventRepository.FieldNames.StackId, new BsonObjectId(new ObjectId(ctx.Event.StackId))))
                .SetSortOrder(SortBy.Descending(EventRepository.FieldNames.Date_UTC))
                .SetFields(EventRepository.FieldNames.Id)
                .SetSkip(maxErrorsPerStack)
                .SetLimit(150)
                .Select(e => new PersistentEvent {
                    Id = e.Id,
                    OrganizationId = ctx.Event.OrganizationId,
                    ProjectId = ctx.Event.ProjectId,
                    StackId = ctx.Event.StackId
                })
                .ToArray();

            if (errors.Length > 0)
                _eventRepository.Delete(errors);
        }
    }
}