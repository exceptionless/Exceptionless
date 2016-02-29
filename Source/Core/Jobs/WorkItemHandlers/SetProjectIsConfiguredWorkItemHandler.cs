using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class SetProjectIsConfiguredWorkItemHandler : WorkItemHandlerBase {
        private readonly IProjectRepository _projectRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ILockProvider _lockProvider;

        public SetProjectIsConfiguredWorkItemHandler(IProjectRepository projectRepository, IEventRepository eventRepository, ICacheClient cacheClient) {
            _projectRepository = projectRepository;
            _eventRepository = eventRepository;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromMinutes(15));
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            return _lockProvider.AcquireAsync(nameof(SetProjectIsConfiguredWorkItemHandler), TimeSpan.FromMinutes(15), new CancellationToken(true));
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