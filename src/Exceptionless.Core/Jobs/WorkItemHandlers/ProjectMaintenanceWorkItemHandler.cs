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
using Foundatio.Messaging;
using Foundatio.Repositories;
using Foundatio.Utility;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class ProjectMaintenanceWorkItemHandler : WorkItemHandlerBase {
        private readonly IProjectRepository _projectRepository;
        private readonly ILockProvider _lockProvider;

        public ProjectMaintenanceWorkItemHandler(IProjectRepository projectRepository, ICacheClient cacheClient, IMessageBus messageBus, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _projectRepository = projectRepository;
            _lockProvider = new CacheLockProvider(cacheClient, messageBus);
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            return _lockProvider.AcquireAsync(nameof(ProjectMaintenanceWorkItemHandler), TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        public override async Task HandleItemAsync(WorkItemContext context) {
            const int LIMIT = 100;

            var workItem = context.GetData<ProjectMaintenanceWorkItem>();
            Log.Info("Received upgrade projects work item. Update Default Bot List: {0} IncrementConfigurationVersion: {1}", workItem.UpdateDefaultBotList, workItem.IncrementConfigurationVersion);

            var results = await _projectRepository.GetAllAsync(o => o.PageLimit(LIMIT)).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                foreach (var project in results.Documents) {
                    if (workItem.UpdateDefaultBotList)
                        project.SetDefaultUserAgentBotPatterns();

                    if (workItem.IncrementConfigurationVersion)
                        project.Configuration.IncrementVersion();
                }

                if (workItem.UpdateDefaultBotList)
                    await _projectRepository.SaveAsync(results.Documents).AnyContext();

                // Sleep so we are not hammering the backend.
                await SystemClock.SleepAsync(TimeSpan.FromSeconds(2.5)).AnyContext();

                if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync().AnyContext())
                    break;

                if (results.Documents.Count > 0)
                    await context.RenewLockAsync().AnyContext();
            }
        }
    }
}