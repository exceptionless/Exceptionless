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
using Exceptionless.Core.Queues;
using Exceptionless.Models.Admin;
using NLog.Fluent;
using ServiceStack.Messaging;

namespace Exceptionless.Core.Pipeline {
    [Priority(70)]
    public class QueueNotificationAction : ErrorPipelineActionBase {
        private readonly IMessageFactory _messageFactory;
        private readonly IProjectHookRepository _projectHookRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IErrorStackRepository _errorStackRepository;
        private readonly IOrganizationRepository _organizationRepository;

        public QueueNotificationAction(IMessageFactory messageFactory, IProjectHookRepository projectHookRepository,
            IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IErrorStackRepository errorStackRepository) {
            _messageFactory = messageFactory;
            _projectHookRepository = projectHookRepository;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _errorStackRepository = errorStackRepository;
        }

        protected override bool ContinueOnError { get { return true; } }

        public override void Process(ErrorPipelineContext ctx) {
            var organization = _organizationRepository.GetByIdCached(ctx.Error.OrganizationId);

            // if they don't have premium features, then we don't need to queue notifications
            if (organization != null && !organization.HasPremiumFeatures)
                return;

            using (IMessageProducer messageProducer = _messageFactory.CreateMessageProducer()) {
                messageProducer.Publish(new ErrorNotification {
                    ErrorId = ctx.Error.Id,
                    ErrorStackId = ctx.Error.ErrorStackId,
                    FullTypeName = ctx.StackingInfo.FullTypeName,
                    IsNew = ctx.IsNew,
                    IsCritical = ctx.Error.Tags != null && ctx.Error.Tags.Contains("Critical"),
                    IsRegression = ctx.IsRegression,
                    Message = ctx.StackingInfo.Message,
                    ProjectId = ctx.Error.ProjectId,
                    Code = ctx.Error.Code,
                    UserAgent = ctx.Error.RequestInfo != null ? ctx.Error.RequestInfo.UserAgent : null,
                    Url = ctx.Error.RequestInfo != null ? ctx.Error.RequestInfo.GetFullPath(true, true) : null
                });

                foreach (ProjectHook hook in _projectHookRepository.GetByProjectId(ctx.Error.ProjectId)) {
                    bool shouldCall = hook.EventTypes.Contains(ProjectHookRepository.EventTypes.NewError) && ctx.IsNew
                                      || hook.EventTypes.Contains(ProjectHookRepository.EventTypes.ErrorRegression) && ctx.IsRegression
                                      || hook.EventTypes.Contains(ProjectHookRepository.EventTypes.CriticalError) && ctx.Error.Tags != null && ctx.Error.Tags.Contains("Critical");

                    if (!shouldCall)
                        continue;

                    Log.Trace().Project(ctx.Error.ProjectId).Message("Web hook queued: project={0} url={1}", ctx.Error.ProjectId, hook.Url).Write();

                    messageProducer.Publish(new WebHookNotification {
                        ProjectId = ctx.Error.ProjectId,
                        Url = hook.Url,
                        Data = WebHookError.FromError(ctx, _projectRepository, _errorStackRepository, _organizationRepository)
                    });
                }
            }
        }
    }
}