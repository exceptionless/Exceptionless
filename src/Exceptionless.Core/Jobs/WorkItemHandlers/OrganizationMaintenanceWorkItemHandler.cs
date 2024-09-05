using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public class OrganizationMaintenanceWorkItemHandler : WorkItemHandlerBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly BillingManager _billingManager;
    private readonly ILockProvider _lockProvider;

    public OrganizationMaintenanceWorkItemHandler(IOrganizationRepository organizationRepository, ICacheClient cacheClient, IMessageBus messageBus, BillingManager billingManager, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _billingManager = billingManager;
        _lockProvider = new CacheLockProvider(cacheClient, messageBus);
    }

    public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new())
    {
        return _lockProvider.AcquireAsync(nameof(OrganizationMaintenanceWorkItemHandler), TimeSpan.FromMinutes(15), cancellationToken);
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        const int LIMIT = 100;
        var wi = context.GetData<OrganizationMaintenanceWorkItem>();
        Log.LogInformation("Received upgrade organizations work item. Upgrade Plans: {UpgradePlans}", wi.UpgradePlans);

        var results = await _organizationRepository.GetAllAsync(o => o.PageLimit(LIMIT));
        while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            foreach (var organization in results.Documents)
            {
                if (wi.UpgradePlans)
                    UpgradePlan(organization);

                if (wi.RemoveOldUsageStats)
                {
                    foreach (var usage in organization.UsageHours.Where(u => u.Date < _timeProvider.GetUtcNow().UtcDateTime.Subtract(TimeSpan.FromDays(3))).ToList())
                        organization.UsageHours.Remove(usage);

                    foreach (var usage in organization.Usage.Where(u => u.Date < _timeProvider.GetUtcNow().UtcDateTime.Subtract(TimeSpan.FromDays(366))).ToList())
                        organization.Usage.Remove(usage);
                }
            }

            if (wi.UpgradePlans || wi.RemoveOldUsageStats)
                await _organizationRepository.SaveAsync(results.Documents);

            // Sleep so we are not hammering the backend.
            await Task.Delay(TimeSpan.FromSeconds(2.5));

            if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync())
                break;

            if (results.Documents.Count > 0)
                await context.RenewLockAsync();
        }

    }

    private void UpgradePlan(Organization organization)
    {
        var plan = _billingManager.GetBillingPlan(organization.PlanId);
        if (plan is null)
        {
            Log.LogError("Unable to find a valid plan for organization: {Organization}", organization.Id);
            return;
        }

        _billingManager.ApplyBillingPlan(organization, plan, user: null, updateBillingPrice: false);
    }
}
