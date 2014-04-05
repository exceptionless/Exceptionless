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
using MongoDB.Driver;
using NLog.Fluent;

namespace Exceptionless.Core.Pipeline {
    [Priority(40)]
    public class SaveEventAction : EventPipelineActionBase {
        private readonly IEventRepository _eventRepository;

        public SaveEventAction(IEventRepository eventRepository) {
            _eventRepository = eventRepository;
        }

        protected override bool IsCritical { get { return true; } }

        public override void Process(EventPipelineContext ctx) {
            try {
                ctx.Event = _eventRepository.Add(ctx.Event);
            } catch (WriteConcernException ex) {
                // ignore errors being submitted multiple times
                if (ex.Message.Contains("E11000")) {
                    Log.Info().Project(ctx.Event.ProjectId).Message("Ignoring duplicate error submission: {0}", ctx.Event.Id).Write();
                    ctx.IsCancelled = true;
                } else
                    throw;
            }
        }
    }
}