#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Threading.Tasks;
using CodeSmith.Core.Component;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Plugins.EventProcessor;

namespace Exceptionless.Core.Pipeline {
    [Priority(80)]
    public class NotifySignalRAction : EventPipelineActionBase {
        private readonly IMessagePublisher _publisher;

        public NotifySignalRAction(IMessagePublisher publisher) {
            _publisher = publisher;
        }

        protected override bool ContinueOnError {
            get { return true; }
        }

        public override void Process(EventContext ctx) {
            Task.Factory.StartNewDelayed(1500, () => _publisher.Publish(new EventOccurrence {
                Id = ctx.Event.Id,
                OrganizationId = ctx.Event.OrganizationId,
                ProjectId = ctx.Event.ProjectId,
                StackId = ctx.Event.StackId,
                Type = ctx.Event.Type,
                IsHidden = ctx.Event.IsHidden,
                IsFixed = ctx.Event.IsFixed,
                IsNotFound = ctx.Event.IsNotFound(),
                IsRegression = ctx.IsRegression
            }));
        }
    }
}