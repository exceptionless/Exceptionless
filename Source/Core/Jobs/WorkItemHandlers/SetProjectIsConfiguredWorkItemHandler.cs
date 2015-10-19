using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Jobs;
using Foundatio.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class SetProjectIsConfiguredWorkItemHandler : WorkItemHandlerBase {
        private readonly IProjectRepository _projectRepository;
        private readonly IEventRepository _eventRepository;

        public SetProjectIsConfiguredWorkItemHandler(IProjectRepository projectRepository, IEventRepository eventRepository) {
            _projectRepository = projectRepository;
            _eventRepository = eventRepository;
        }
        
        public override async Task HandleItemAsync(WorkItemContext context) {
            var workItem = context.GetData<SetProjectIsConfiguredWorkItem>();
            Logger.Info().Message($"Setting Is Configured for project: {workItem.ProjectId}").Write();
            
            var project = await _projectRepository.GetByIdAsync(workItem.ProjectId).AnyContext();
            if (project == null || project.IsConfigured.GetValueOrDefault())
                return;

            project.IsConfigured = workItem.IsConfigured || await _eventRepository.GetCountByProjectIdAsync(project.Id).AnyContext() > 0;
            await _projectRepository.SaveAsync(project, true).AnyContext();
        }
    }
}