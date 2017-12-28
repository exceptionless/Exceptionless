using System;
using System.Linq;
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
using Foundatio.Messaging;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class OrganizationMaintenanceWorkItemHandler : WorkItemHandlerBase {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly ILockProvider _lockProvider;

        public OrganizationMaintenanceWorkItemHandler(IOrganizationRepository organizationRepository, ICacheClient cacheClient, IMessageBus messageBus, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _organizationRepository = organizationRepository;
            _lockProvider = new CacheLockProvider(cacheClient, messageBus);
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            return _lockProvider.AcquireAsync(nameof(OrganizationMaintenanceWorkItemHandler), TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        public override async Task HandleItemAsync(WorkItemContext context) {
            const int LIMIT = 100;
            var wi = context.GetData<OrganizationMaintenanceWorkItem>();
            Log.LogInformation("Received upgrade organizations work item. Upgrade Plans: {UpgradePlans}", wi.UpgradePlans);

            var results = await _organizationRepository.GetAllAsync(o => o.PageLimit(LIMIT)).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                foreach (var organization in results.Documents) {
                    if (wi.UpgradePlans)
                        UpgradePlan(organization);

                    if (wi.RemoveOldUsageStats) {
                        foreach (var usage in organization.OverageHours.Where(u => u.Date < SystemClock.UtcNow.Subtract(TimeSpan.FromDays(3))).ToList())
                            organization.OverageHours.Remove(usage);

                        foreach (var usage in organization.Usage.Where(u => u.Date < SystemClock.UtcNow.Subtract(TimeSpan.FromDays(366))).ToList())
                            organization.Usage.Remove(usage);
                    }
                }

                if (wi.UpgradePlans || wi.RemoveOldUsageStats)
                    await _organizationRepository.SaveAsync(results.Documents).AnyContext();

                // Sleep so we are not hammering the backend.
                await SystemClock.SleepAsync(TimeSpan.FromSeconds(2.5)).AnyContext();

                if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync().AnyContext())
                    break;

                if (results.Documents.Count > 0)
                    await context.RenewLockAsync().AnyContext();
            }
            
        }

        private void UpgradePlan(Organization organization) {
            var plan = BillingManager.GetBillingPlan(organization.PlanId);
            if (plan == null) {
                Log.LogError("Unable to find a valid plan for organization: {organization}", organization.Id);
                return;
            }

            BillingManager.ApplyBillingPlan(organization, plan, user: null, updateBillingPrice: false);
        }
    }
}