#region Copyright 2015 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models.Admin;
using Foundatio.Queues;
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

            if (ShouldQueueNotification(ctx))
                _notificationQueue.Enqueue(new EventNotification {
                    Event = ctx.Event,
                    IsNew = ctx.IsNew,
                    IsCritical = ctx.Event.IsCritical(),
                    IsRegression = ctx.IsRegression,
                    TotalOccurrences = ctx.Stack.TotalOccurrences,
                    ProjectName = ctx.Project.Name
                });

            foreach (WebHook hook in _webHookRepository.GetByOrganizationIdOrProjectId(ctx.Event.OrganizationId, ctx.Event.ProjectId)) {
                if (!ShouldCallWebHook(hook, ctx))
                    continue;

                var context = new WebHookDataContext(hook.Version, ctx.Event, ctx.Organization, ctx.Project, ctx.Stack, ctx.IsNew, ctx.IsRegression);
                _webHookNotificationQueue.Enqueue(new WebHookNotification {
                    OrganizationId = ctx.Event.OrganizationId,
                    ProjectId = ctx.Event.ProjectId, 
                    Url = hook.Url,
                    Data = _webHookDataPluginManager.CreateFromEvent(context)
                });

                Log.Trace().Project(ctx.Event.ProjectId).Message("Web hook queued: project={0} url={1}", ctx.Event.ProjectId, hook.Url).Write();
            }
        }

        private bool ShouldCallWebHook(WebHook hook, EventContext ctx) {
            if (ctx.IsNew && ctx.Event.IsError() && hook.EventTypes.Contains(WebHookRepository.EventTypes.NewError))
                return true;

            if (ctx.Event.IsCritical() && ctx.Event.IsError() && hook.EventTypes.Contains(WebHookRepository.EventTypes.CriticalError))
                return true;

            if (ctx.IsRegression && hook.EventTypes.Contains(WebHookRepository.EventTypes.StackRegression))
                return true;

            if (ctx.IsNew && hook.EventTypes.Contains(WebHookRepository.EventTypes.NewEvent))
                return true;

            if (ctx.Event.IsCritical() && hook.EventTypes.Contains(WebHookRepository.EventTypes.CriticalEvent))
                return true;

            return false;
        }

        private bool ShouldQueueNotification(EventContext ctx) {
            if (ctx.Project.NotificationSettings.Count == 0)
                return false;

            if (ctx.IsNew && ctx.Event.IsError() && ctx.Project.NotificationSettings.Any(n => n.Value.ReportNewErrors))
                return true;

            if (ctx.Event.IsCritical() && ctx.Event.IsError() && ctx.Project.NotificationSettings.Any(n => n.Value.ReportCriticalErrors))
                return true;

            if (ctx.IsRegression && ctx.Project.NotificationSettings.Any(n => n.Value.ReportEventRegressions))
                return true;

            if (ctx.IsNew && ctx.Project.NotificationSettings.Any(n => n.Value.ReportNewEvents))
                return true;

            if (ctx.Event.IsCritical() && ctx.Project.NotificationSettings.Any(n => n.Value.ReportCriticalEvents))
                return true;

            return false;
        }
    }
}