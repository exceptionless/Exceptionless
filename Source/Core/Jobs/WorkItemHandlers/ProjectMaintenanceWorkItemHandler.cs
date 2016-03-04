using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Logging;
using Foundatio.Messaging;
using Foundatio.Repositories.Models;

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
            _logger.Info().Message("Received upgrade projects work item. Update Default Bot List: {0}", workItem.UpdateDefaultBotList).Write();

            var results = await _projectRepository.GetAllAsync(paging: new PagingOptions().WithLimit(LIMIT)).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                foreach (var project in results.Documents) {
                    if (workItem.UpdateDefaultBotList)
                        project.SetDefaultUserAgentBotPatterns();
                }

                if (workItem.UpdateDefaultBotList)
                    await _projectRepository.SaveAsync(results.Documents).AnyContext();

                // Sleep so we are not hammering the backend.
                await Task.Delay(TimeSpan.FromSeconds(2.5)).AnyContext();

                await results.NextPageAsync().AnyContext();
                if (results.Documents.Count > 0)
                    await context.WorkItemLock.RenewAsync().AnyContext();
            }
        }

        private void UpgradePlan(Organization organization) {
            var plan = BillingManager.GetBillingPlan(organization.PlanId);
            if (plan == null) {
                _logger.Error().Message("Unable to find a valid plan for organization: {0}", organization.Id).Write();
                return;
            }

            BillingManager.ApplyBillingPlan(organization, plan, user: null, updateBillingPrice: false);
        }
    }
}