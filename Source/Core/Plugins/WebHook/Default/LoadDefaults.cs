using System;
using CodeSmith.Core.Component;
using Exceptionless.Core.Repositories;

namespace Exceptionless.Core.Plugins.WebHook {
    [Priority(0)]
    public class LoadDefaults : WebHookDataPluginBase {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;

        public LoadDefaults(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IStackRepository stackRepository) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
        }

        public override object CreateFromEvent(WebHookDataContext ctx) {
            if (ctx.Event == null)
                throw new ArgumentException("Event cannot be null.");

            if (ctx.Project == null)
                ctx.Project = _projectRepository.GetById(ctx.Event.ProjectId, true);

            if (ctx.Project == null)
                throw new ArgumentException("Project not found.");

            if (ctx.Organization == null)
                ctx.Organization = _organizationRepository.GetById(ctx.Event.OrganizationId);

            if (ctx.Organization == null)
                throw new ArgumentException("Organization not found.");

            if (ctx.Stack == null)
                ctx.Stack = _stackRepository.GetById(ctx.Event.StackId);

            if (ctx.Stack == null)
                throw new ArgumentException("Stack not found.");

            return null;
        }

        public override object CreateFromStack(WebHookDataContext ctx) {
            if (ctx.Stack == null)
                throw new ArgumentException("Stack cannot be null.");

            if (ctx.Project == null)
                ctx.Project = _projectRepository.GetById(ctx.Stack.ProjectId, true);

            if (ctx.Project == null)
                throw new ArgumentException("Project not found.");

            if (ctx.Organization == null)
                ctx.Organization = _organizationRepository.GetById(ctx.Stack.OrganizationId);

            if (ctx.Organization == null)
                throw new ArgumentException("Organization not found.");

            return null;
        }
    }
}