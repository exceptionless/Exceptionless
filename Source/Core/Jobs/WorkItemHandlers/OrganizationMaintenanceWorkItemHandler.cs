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
    public class OrganizationMaintenanceWorkItemHandler : WorkItemHandlerBase {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly ILockProvider _lockProvider;

        public OrganizationMaintenanceWorkItemHandler(IOrganizationRepository organizationRepository, ICacheClient cacheClient, IMessageBus messageBus) {
            _organizationRepository = organizationRepository;
            _lockProvider = new CacheLockProvider(cacheClient, messageBus);
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            return _lockProvider.AcquireAsync(nameof(OrganizationMaintenanceWorkItemHandler), TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        public override async Task HandleItemAsync(WorkItemContext context) {
            const int LIMIT = 100;

            var workItem = context.GetData<OrganizationMaintenanceWorkItem>();
            Logger.Info().Message($"Received upgrade organizations work item. Upgrade Plans: {workItem.UpgradePlans}").Write();

            var results = await _organizationRepository.GetAllAsync(paging: new PagingOptions().WithLimit(LIMIT)).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                foreach (var organization in results.Documents) {
                    if (workItem.UpgradePlans)
                        UpgradePlan(organization);
                }

                if (workItem.UpgradePlans)
                    await _organizationRepository.SaveAsync(results.Documents).AnyContext();

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
                Logger.Error().Message($"Unable to find a valid plan for organization: {organization.Id}").Write();
                return;
            }

            BillingManager.ApplyBillingPlan(organization, plan, user: null, updateBillingPrice: false);
        }
    }
}