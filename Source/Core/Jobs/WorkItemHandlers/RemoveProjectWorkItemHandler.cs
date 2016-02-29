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
    public class RemoveProjectWorkItemHandler : WorkItemHandlerBase {
        private readonly IProjectRepository _projectRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IStackRepository _stackRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly IWebHookRepository _webHookRepository;
        private readonly ILockProvider _lockProvider;

        public RemoveProjectWorkItemHandler(IProjectRepository projectRepository, IEventRepository eventRepository, IStackRepository stackRepository, ITokenRepository tokenRepository, IWebHookRepository webHookRepository, ICacheClient cacheClient) {
            _projectRepository = projectRepository;
            _eventRepository = eventRepository;
            _stackRepository = stackRepository;
            _tokenRepository = tokenRepository;
            _webHookRepository = webHookRepository;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromMinutes(15));
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            return _lockProvider.AcquireAsync(nameof(RemoveProjectWorkItemHandler), TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        public override async Task HandleItemAsync(WorkItemContext context) {
            var workItem = context.GetData<RemoveProjectWorkItem>();
            Logger.Info().Message($"Received remove project work item for: {workItem.ProjectId} Reset Data: {workItem.Reset}").Write();

            await context.ReportProgressAsync(0, "Starting deletion...").AnyContext();
            var project = await _projectRepository.GetByIdAsync(workItem.ProjectId).AnyContext();
            if (project == null) {
                await context.ReportProgressAsync(100, workItem.Reset ? "Project data reset" : "Project deleted").AnyContext();
                return;
            }

            if (!workItem.Reset) {
                await context.ReportProgressAsync(20, "Removing tokens").AnyContext();
                await _tokenRepository.RemoveAllByOrganizationIdsAsync(new[] { project.Id }).AnyContext();

                await context.ReportProgressAsync(40, "Removing web hooks").AnyContext();
                await _webHookRepository.RemoveAllByOrganizationIdsAsync(new[] { project.Id }).AnyContext();
            }

            await context.ReportProgressAsync(60, "Resetting project data").AnyContext();
            await _eventRepository.RemoveAllByProjectIdsAsync(new[] { project.Id }).AnyContext();
            await _stackRepository.RemoveAllByProjectIdsAsync(new[] { project.Id }).AnyContext();

            if (!workItem.Reset) {
                await context.ReportProgressAsync(80, "Removing project").AnyContext();
                await _projectRepository.RemoveAsync(project.Id).AnyContext();
            }

            await context.ReportProgressAsync(100, workItem.Reset ? "Project data reset" : "Project deleted").AnyContext();
        }
    }
}