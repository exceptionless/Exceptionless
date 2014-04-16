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
using ServiceStack.Redis;

namespace Exceptionless.Core.Pipeline {
    [Priority(80)]
    public class NotifySignalRAction : EventPipelineActionBase {
        public const string NOTIFICATION_CHANNEL_KEY = "notifications:signalr";

        private readonly IRedisClientsManager _clientsManager;

        public NotifySignalRAction(IRedisClientsManager clientsManager) {
            _clientsManager = clientsManager;
        }

        protected override bool ContinueOnError { get { return true; } }

        public override void Process(EventContext ctx) {
            using (IRedisClient client = _clientsManager.GetClient())
                client.PublishMessage(NOTIFICATION_CHANNEL_KEY, String.Concat(ctx.Event.OrganizationId, ":", ctx.Event.ProjectId, ":", ctx.Event.StackId, ":", ctx.Event.IsHidden, ":", ctx.Event.IsFixed, ":", ctx.Event.IsNotFound));
        }
    }
}