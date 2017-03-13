using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;

namespace Exceptionless.Core.Plugins.WebHook {
    [Priority(0)]
    public sealed class LoadDefaults : WebHookDataPluginBase {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;

        public LoadDefaults(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IStackRepository stackRepository) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
        }

        public override async Task<object> CreateFromEventAsync(WebHookDataContext ctx) {
            if (ctx.Event == null)
                throw new ArgumentException("Event cannot be null.");

            if (ctx.Project == null)
                ctx.Project = await _projectRepository.GetByIdAsync(ctx.Event.ProjectId, o => o.Cache()).AnyContext();

            if (ctx.Project == null)
                throw new ArgumentException("Project not found.");

            if (ctx.Organization == null)
                ctx.Organization = await _organizationRepository.GetByIdAsync(ctx.Event.OrganizationId, o => o.Cache()).AnyContext();

            if (ctx.Organization == null)
                throw new ArgumentException("Organization not found.");

            if (ctx.Stack == null)
                ctx.Stack = await _stackRepository.GetByIdAsync(ctx.Event.StackId).AnyContext();

            if (ctx.Stack == null)
                throw new ArgumentException("Stack not found.");

            return null;
        }

        public override async Task<object> CreateFromStackAsync(WebHookDataContext ctx) {
            if (ctx.Stack == null)
                throw new ArgumentException("Stack cannot be null.");

            if (ctx.Project == null)
                ctx.Project = await _projectRepository.GetByIdAsync(ctx.Stack.ProjectId, o => o.Cache()).AnyContext();

            if (ctx.Project == null)
                throw new ArgumentException("Project not found.");

            if (ctx.Organization == null)
                ctx.Organization = await _organizationRepository.GetByIdAsync(ctx.Stack.OrganizationId, o => o.Cache()).AnyContext();

            if (ctx.Organization == null)
                throw new ArgumentException("Organization not found.");

            return null;
        }
    }
}