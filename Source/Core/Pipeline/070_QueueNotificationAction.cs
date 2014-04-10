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
using Exceptionless.Core.Models;
using Exceptionless.Core.EventPlugins;
using Exceptionless.Core.Queues;
using Exceptionless.Models.Admin;
using NLog.Fluent;
using ServiceStack.Messaging;

namespace Exceptionless.Core.Pipeline {
    [Priority(70)]
    public class QueueNotificationAction : EventPipelineActionBase {
        private readonly IMessageFactory _messageFactory;
        private readonly IProjectHookRepository _projectHookRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IOrganizationRepository _organizationRepository;

        public QueueNotificationAction(IMessageFactory messageFactory, IProjectHookRepository projectHookRepository,
            IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IStackRepository stackRepository) {
            _messageFactory = messageFactory;
            _projectHookRepository = projectHookRepository;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
        }

        protected override bool ContinueOnError { get { return true; } }

        public override void Process(EventContext ctx) {
            // if they don't have premium features, then we don't need to queue notifications
            if (!ctx.Organization.HasPremiumFeatures)
                return;

            using (IMessageProducer messageProducer = _messageFactory.CreateMessageProducer()) {
                messageProducer.Publish(new EventNotification {
                    Event = ctx.Event,
                    IsNew = ctx.IsNew,
                    IsCritical = ctx.Event.IsCritical,
                    IsRegression = ctx.IsRegression,
                    TotalOccurrences = ctx.Stack.TotalOccurrences,
                    ProjectName = ctx.Project.Name
                });

                foreach (ProjectHook hook in _projectHookRepository.GetByProjectId(ctx.Event.ProjectId)) {
                    bool shouldCall = hook.EventTypes.Contains(ProjectHookRepository.EventTypes.NewError) && ctx.IsNew
                                      || hook.EventTypes.Contains(ProjectHookRepository.EventTypes.ErrorRegression) && ctx.IsRegression
                                      || hook.EventTypes.Contains(ProjectHookRepository.EventTypes.CriticalError) && ctx.Event.Tags != null && ctx.Event.Tags.Contains("Critical");

                    if (!shouldCall)
                        continue;

                    Log.Trace().Project(ctx.Event.ProjectId).Message("Web hook queued: project={0} url={1}", ctx.Event.ProjectId, hook.Url).Write();

                    messageProducer.Publish(new WebHookNotification {
                        ProjectId = ctx.Event.ProjectId,
                        Url = hook.Url,
                        Data = WebHookEvent.FromEvent(ctx, _projectRepository, _stackRepository, _organizationRepository)
                    });
                }
            }
        }
    }
}