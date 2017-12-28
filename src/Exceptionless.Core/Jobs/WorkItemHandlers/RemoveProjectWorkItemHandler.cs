using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class RemoveProjectWorkItemHandler : WorkItemHandlerBase {
        private readonly IProjectRepository _projectRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IStackRepository _stackRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly IWebHookRepository _webHookRepository;
        private readonly ILockProvider _lockProvider;

        public RemoveProjectWorkItemHandler(IProjectRepository projectRepository, IEventRepository eventRepository, IStackRepository stackRepository, ITokenRepository tokenRepository, IWebHookRepository webHookRepository, ICacheClient cacheClient, IMessageBus messageBus, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _projectRepository = projectRepository;
            _eventRepository = eventRepository;
            _stackRepository = stackRepository;
            _tokenRepository = tokenRepository;
            _webHookRepository = webHookRepository;
            _lockProvider = new CacheLockProvider(cacheClient, messageBus);
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            string cacheKey = $"{nameof(RemoveProjectWorkItemHandler)}:{((RemoveProjectWorkItem)workItem).ProjectId}";
            return _lockProvider.AcquireAsync(cacheKey, TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        public override async Task HandleItemAsync(WorkItemContext context) {
            var wi = context.GetData<RemoveProjectWorkItem>();
            using (Log.BeginScope(new ExceptionlessState().Organization(wi.OrganizationId).Project(wi.ProjectId))) {
                Log.LogInformation("Received remove project work item for: {0} Reset Data: {1}", wi.ProjectId, wi.Reset);

                await context.ReportProgressAsync(0, "Starting deletion...").AnyContext();
                var project = await _projectRepository.GetByIdAsync(wi.ProjectId).AnyContext();
                if (project == null) {
                    await context.ReportProgressAsync(100, wi.Reset ? "Project data reset" : "Project deleted").AnyContext();
                    return;
                }

                if (!wi.Reset) {
                    await context.ReportProgressAsync(20, "Removing tokens").AnyContext();
                    await _tokenRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id).AnyContext();

                    await context.ReportProgressAsync(40, "Removing web hooks").AnyContext();
                    await _webHookRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id).AnyContext();
                }

                await context.ReportProgressAsync(60, "Resetting project data").AnyContext();
                await _eventRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id).AnyContext();
                await _stackRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id).AnyContext();

                if (!wi.Reset) {
                    await context.ReportProgressAsync(80, "Removing project").AnyContext();
                    await _projectRepository.RemoveAsync(project.Id).AnyContext();
                }

                await context.ReportProgressAsync(100, wi.Reset ? "Project data reset" : "Project deleted").AnyContext();
            }
        }
    }
}