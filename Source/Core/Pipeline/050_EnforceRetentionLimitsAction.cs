#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CodeSmith.Core.Component;
using Exceptionless.Core.Plugins.EventPipeline;
using Exceptionless.Core.Repositories;
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

            int maxEventsPerStack = ctx.Organization.MaxEventsPerDay > 0 ? ctx.Organization.MaxEventsPerDay + Math.Min(50, ctx.Organization.MaxEventsPerDay * 2) : Int32.MaxValue;

            // Get a list of oldest ids that exceed our desired max events.
            var options = new PagingOptions { Limit = maxEventsPerStack, Page = 2 };
            IList<string> ids = _eventRepository.GetExceededRetentionEventIds(ctx.Event.StackId, options);
            while (ids.Count > 0) {
                var eventsToRemove = ids.Select(id => new PersistentEvent {
                    Id = id,
                    OrganizationId = ctx.Event.OrganizationId,
                    ProjectId = ctx.Event.ProjectId,
                    StackId = ctx.Event.StackId
                }).ToList();

                _eventRepository.Remove(eventsToRemove);

                if (!options.HasMore)
                    break;

                ids = _eventRepository.GetExceededRetentionEventIds(ctx.Event.StackId, options);
            }
        }
    }
}