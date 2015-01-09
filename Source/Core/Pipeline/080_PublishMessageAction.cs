#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Plugins.EventProcessor;

namespace Exceptionless.Core.Pipeline {
    [Priority(80)]
    public class PublishMessageAction : EventPipelineActionBase {
        private readonly IMessagePublisher _publisher;

        public PublishMessageAction(IMessagePublisher publisher) {
            _publisher = publisher;
        }

        protected override bool ContinueOnError {
            get { return true; }
        }

        public override void ProcessBatch(ICollection<EventContext> contexts) {
            Task.Factory.StartNewDelayed(1500, () => {
                foreach (var ctx in contexts.GroupBy(ctx => ctx.Event.ProjectId))
                    _publisher.Publish(new EventOccurrence {
                        Ids = new List<string>(ctx.Select(c => c.Event.Id)),
                        OrganizationId = ctx.First().Event.OrganizationId,
                        ProjectId = ctx.Key,
                        StackIds = new List<string>(ctx.Select(c => c.Event.StackId)),
                    });
            });
        }

        public override void Process(EventContext ctx) {}
    }
}