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
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Models.Admin;
using NLog.Fluent;

namespace Exceptionless.Core.Pipeline {
    [Priority(70)]
    public class QueueNotificationAction : EventPipelineActionBase {
        private readonly IQueue<EventNotification> _notificationQueue;
        private readonly IQueue<WebHookNotification> _webHookNotificationQueue;
        private readonly IWebHookRepository _webHookRepository;
        private readonly WebHookDataPluginManager _webHookDataPluginManager;

        public QueueNotificationAction(IQueue<EventNotification> notificationQueue, 
            IQueue<WebHookNotification> webHookNotificationQueue, 
            IWebHookRepository webHookRepository,
            WebHookDataPluginManager webHookDataPluginManager) {
            _notificationQueue = notificationQueue;
            _webHookNotificationQueue = webHookNotificationQueue;
            _webHookRepository = webHookRepository;
            _webHookDataPluginManager = webHookDataPluginManager;
        }

        protected override bool ContinueOnError { get { return true; } }

        public override void Process(EventContext ctx) {
            // if they don't have premium features, then we don't need to queue notifications
            if (!ctx.Organization.HasPremiumFeatures)
                return;

            _notificationQueue.Enqueue(new EventNotification {
                Event = ctx.Event,
                IsNew = ctx.IsNew,
                IsCritical = ctx.Event.IsCritical(),
                IsRegression = ctx.IsRegression,
                //TotalOccurrences = ctx.Stack.TotalOccurrences,
                ProjectName = ctx.Project.Name
            });

            foreach (WebHook hook in _webHookRepository.GetByOrganizationIdOrProjectId(ctx.Event.OrganizationId, ctx.Event.ProjectId)) {
                bool shouldCall = hook.EventTypes.Contains(WebHookRepository.EventTypes.NewError) && ctx.IsNew
                                  || hook.EventTypes.Contains(WebHookRepository.EventTypes.ErrorRegression) && ctx.IsRegression
                                  || hook.EventTypes.Contains(WebHookRepository.EventTypes.CriticalError) && ctx.Event.Tags != null && ctx.Event.Tags.Contains("Critical");

                if (!shouldCall)
                    continue;

                Log.Trace().Project(ctx.Event.ProjectId).Message("Web hook queued: project={0} url={1}", ctx.Event.ProjectId, hook.Url).Write();

                // TODO: Should we be using the hook's project id and organization id?
                var context = new WebHookDataContext(hook.Version, ctx.Event, ctx.Organization, ctx.Project, ctx.Stack, ctx.IsNew, ctx.IsRegression);
                _webHookNotificationQueue.Enqueue(new WebHookNotification {
                    OrganizationId = ctx.Event.OrganizationId,
                    ProjectId = ctx.Event.ProjectId, 
                    Url = hook.Url,
                    Data = _webHookDataPluginManager.CreateFromEvent(context)
                });
            }
        }
    }
}