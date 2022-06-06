using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Extensions;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

public sealed class UsageService {
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICacheClient _cache;
    private readonly IMessagePublisher _messagePublisher;
    private readonly BillingPlans _plans;
    private readonly ILogger<UsageService> _logger;

    public UsageService(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, ICacheClient cache, IMessagePublisher messagePublisher, BillingPlans plans, ILoggerFactory loggerFactory = null) {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _cache = cache;
        _messagePublisher = messagePublisher;
        _plans = plans;
        _logger = loggerFactory.CreateLogger<UsageService>();
    }

    public async Task<bool> IsOverLimitAsync(Organization organization) {
        if (organization is null || organization.MaxEventsPerMonth < 0)
            return false;

        if (organization.IsSuspended)
            return true;

        // TODO: Simplify this by removing blocked count from total.. We also need a migration for this.
        int monthlyTotal = await _cache.GetAsync(GetMonthlyTotalCacheKey(organization.Id), organization.GetCurrentMonthlyTotal());
        int monthlyBlocked = await _cache.GetAsync(GetMonthlyBlockedCacheKey(organization.Id), organization.GetCurrentMonthlyBlocked());

        int hourlyEventLimit = organization.GetHourlyEventLimit(monthlyTotal, monthlyBlocked, _plans.FreePlan.Id);
        int monthlyEventLimit = organization.GetMaxEventsPerMonthWithBonus();
        double originalAllowedMonthlyEventTotal = monthlyTotal - monthlyBlocked;

        // If the original count is less than the max events per month and original count + hourly limit is greater than the max events per month then use the monthly limit.
        if (originalAllowedMonthlyEventTotal < monthlyEventLimit && (originalAllowedMonthlyEventTotal + hourlyEventLimit) >= monthlyEventLimit)
            return originalAllowedMonthlyEventTotal < monthlyEventLimit && Math.Max(monthlyTotal - monthlyBlocked - monthlyEventLimit, 0) > 0;

        int hourlyTotal = await _cache.GetAsync(GetHourlyTotalCacheKey(organization.Id), organization.GetCurrentHourlyTotal());
        int hourlyBlocked = await _cache.GetAsync(GetHourlyBlockedCacheKey(organization.Id), organization.GetCurrentHourlyBlocked());

        double originalAllowedHourlyEventTotal = hourlyTotal - hourlyBlocked;
        if ((hourlyTotal - hourlyBlocked) > hourlyEventLimit)
            return originalAllowedHourlyEventTotal < hourlyEventLimit && Math.Max(hourlyTotal - hourlyBlocked - hourlyEventLimit, 0) > 0;

        if ((monthlyTotal - monthlyBlocked) > monthlyEventLimit)
            return originalAllowedMonthlyEventTotal < monthlyEventLimit && Math.Max(monthlyTotal - monthlyBlocked - monthlyEventLimit, 0) > 0;

        return false;
    }

    private readonly TimeSpan _hourlyUsageBucketTimeToLive = TimeSpan.FromMinutes(61);
    private readonly TimeSpan _monthlyUsageBucketTimeToLive = TimeSpan.FromDays(32);

    public Task IncrementTotalAsync(Organization organization, Project project, int count = 1) {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive number");

        var bucket = SystemClock.UtcNow.Floor(TimeSpan.FromMinutes(15));
        return Task.WhenAll(
            _cache.IncrementAsync(GetHourlyTotalCacheKey(organization.Id), count, _hourlyUsageBucketTimeToLive, organization.GetCurrentHourlyTotal()),
            _cache.IncrementAsync(GetHourlyTotalCacheKey(organization.Id, project.Id), count, _hourlyUsageBucketTimeToLive, project.GetCurrentHourlyTotal()),
            _cache.IncrementAsync(GetMonthlyTotalCacheKey(organization.Id), count, _monthlyUsageBucketTimeToLive, organization.GetCurrentMonthlyTotal()),
            _cache.IncrementAsync(GetMonthlyTotalCacheKey(organization.Id, project.Id), count, _monthlyUsageBucketTimeToLive, project.GetCurrentMonthlyTotal()),
            QueueSaveUsageAsync(organization, project, bucket.AddMinutes(15))
        );
    }

    public Task IncrementBlockedAsync(Organization organization, Project project, int count = 1) {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive number");

        var bucket = SystemClock.UtcNow.Floor(TimeSpan.FromMinutes(15));
        return Task.WhenAll(
            _cache.IncrementAsync(GetHourlyBlockedCacheKey(organization.Id), count, _hourlyUsageBucketTimeToLive, organization.GetCurrentHourlyBlocked()),
            _cache.IncrementAsync(GetHourlyBlockedCacheKey(organization.Id, project.Id), count, _hourlyUsageBucketTimeToLive, project.GetCurrentHourlyBlocked()),
            _cache.IncrementAsync(GetMonthlyBlockedCacheKey(organization.Id), count, _monthlyUsageBucketTimeToLive, organization.GetCurrentMonthlyBlocked()),
            _cache.IncrementAsync(GetMonthlyBlockedCacheKey(organization.Id, project.Id), count, _monthlyUsageBucketTimeToLive, project.GetCurrentMonthlyBlocked()),
            QueueSaveUsageAsync(organization, project, bucket.AddMinutes(15))
        );
    }

    public Task IncrementTooBigAsync(Organization organization, Project project, int count = 1) {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive number");

        var bucket = SystemClock.UtcNow.Floor(TimeSpan.FromMinutes(15));
        return Task.WhenAll(
            _cache.IncrementAsync(GetHourlyTooBigCacheKey(organization.Id), count, _hourlyUsageBucketTimeToLive, organization.GetCurrentHourlyTooBig()),
            _cache.IncrementAsync(GetHourlyTooBigCacheKey(organization.Id, project.Id), count, _hourlyUsageBucketTimeToLive, project.GetCurrentHourlyTooBig()),
            _cache.IncrementAsync(GetMonthlyTooBigCacheKey(organization.Id), count, _monthlyUsageBucketTimeToLive, organization.GetCurrentMonthlyTooBig()),
            _cache.IncrementAsync(GetMonthlyTooBigCacheKey(organization.Id, project.Id), count, _monthlyUsageBucketTimeToLive, project.GetCurrentMonthlyTooBig()),
            QueueSaveUsageAsync(organization, project, bucket.AddMinutes(15))
        );
    }

    private Task<long> QueueSaveUsageAsync(Organization organization, Project project, DateTime nextSaveUtc) {
        return _cache.ListAddAsync("usage:save", new[] {
            new SaveUsage { OrganizationId = organization.Id, NextSaveUtc = nextSaveUtc },
            new SaveUsage { OrganizationId = organization.Id, ProjectId = project.Id, NextSaveUtc = nextSaveUtc }
        }, _hourlyUsageBucketTimeToLive);
    }

    public async Task<bool> IncrementUsageAsync(Organization organization, Project project, int count = 1, bool applyHourlyLimit = true) {
        bool justWentOverHourly = orgUsage.HourlyTotal > organization.GetHourlyEventLimit(orgUsage.MonthlyTotal, orgUsage.MonthlyBlocked, _plans.FreePlan.Id) && orgUsage.HourlyTotal <= organization.GetHourlyEventLimit(orgUsage.MonthlyTotal, orgUsage.MonthlyBlocked, _plans.FreePlan.Id) + count;
        bool justWentOverMonthly = orgUsage.MonthlyTotal > organization.GetMaxEventsPerMonthWithBonus() && orgUsage.MonthlyTotal <= organization.GetMaxEventsPerMonthWithBonus() + count;
        var projectUsage = await IncrementUsageAsync(organization, project, tooBig, count, overLimit, (int)totalBlocked).AnyContext();

        var tasks = new List<Task>(3) {
            SaveUsageAsync(organization, justWentOverHourly, justWentOverMonthly, orgUsage),
            SaveUsageAsync(organization, project, justWentOverHourly, justWentOverMonthly, projectUsage)
        };

        if (justWentOverMonthly)
            tasks.Add(_messagePublisher.PublishAsync(new PlanOverage { OrganizationId = organization.Id }));
        else if (justWentOverHourly)
            tasks.Add(_messagePublisher.PublishAsync(new PlanOverage { OrganizationId = organization.Id, IsBucket = true }));

        await Task.WhenAll(tasks).AnyContext();
        return overLimit;
    }

    public async Task<Usage> GetUsageAsync(Organization org) {
        var hourlyUsage = org.GetLatestOverage();
        var monthlyUsage = org.GetCurrentMonthlyUsage();

        var hourlyTotal = _cache.GetAsync(GetHourlyTotalCacheKey(org.Id), hourlyUsage.Total);
        var monthlyTotal = _cache.GetAsync(GetMonthlyTotalCacheKey(org.Id), monthlyUsage.Total);
        var hourlyTooBig = _cache.GetAsync(GetHourlyTooBigCacheKey(org.Id), hourlyUsage.TooBig);
        var monthlyTooBig = _cache.GetAsync(GetMonthlyTooBigCacheKey(org.Id), monthlyUsage.TooBig);
        var hourlyBlocked = _cache.GetAsync(GetHourlyBlockedCacheKey(org.Id), hourlyUsage.Blocked);
        var monthlyBlocked = _cache.GetAsync(GetMonthlyBlockedCacheKey(org.Id), monthlyUsage.Blocked);
        await Task.WhenAll(hourlyTotal, monthlyTotal, hourlyTooBig, monthlyTooBig, hourlyBlocked, monthlyBlocked).AnyContext();

        return new Usage {
            HourlyTotal = hourlyTotal.Result,
            MonthlyTotal = monthlyTotal.Result,
            HourlyTooBig = hourlyTooBig.Result,
            MonthlyTooBig = monthlyTooBig.Result,
            HourlyBlocked = hourlyBlocked.Result,
            MonthlyBlocked = monthlyBlocked.Result,
        };
    }

    public async Task<Usage> GetUsageAsync(Organization org, Project project) {
        var hourlyUsage = project.GetCurrentHourlyUsage();
        var monthlyUsage = project.GetCurrentMonthlyUsage();

        var hourlyTotal = _cache.GetAsync(GetHourlyTotalCacheKey(org.Id, project.Id), hourlyUsage.Total);
        var monthlyTotal = _cache.GetAsync(GetMonthlyTotalCacheKey(org.Id, project.Id), monthlyUsage.Total);
        var hourlyTooBig = _cache.GetAsync(GetHourlyTooBigCacheKey(org.Id, project.Id), hourlyUsage.TooBig);
        var monthlyTooBig = _cache.GetAsync(GetMonthlyTooBigCacheKey(org.Id, project.Id), monthlyUsage.TooBig);
        var hourlyBlocked = _cache.GetAsync(GetHourlyBlockedCacheKey(org.Id, project.Id), hourlyUsage.Blocked);
        var monthlyBlocked = _cache.GetAsync(GetMonthlyBlockedCacheKey(org.Id, project.Id), monthlyUsage.Blocked);
        await Task.WhenAll(hourlyTotal, monthlyTotal, hourlyTooBig, monthlyTooBig, hourlyBlocked, monthlyBlocked).AnyContext();

        return new Usage {
            HourlyTotal = hourlyTotal.Result,
            MonthlyTotal = monthlyTotal.Result,
            HourlyTooBig = hourlyTooBig.Result,
            MonthlyTooBig = monthlyTooBig.Result,
            HourlyBlocked = hourlyBlocked.Result,
            MonthlyBlocked = monthlyBlocked.Result,
        };
    }

    private async Task SaveUsageAsync(Organization org, bool justWentOverHourly, bool justWentOverMonthly, Usage usage) {
        bool shouldSaveUsage = await ShouldSaveUsageAsync(org, null, justWentOverHourly, justWentOverMonthly).AnyContext();
        if (!shouldSaveUsage)
            return;

        string orgId = org.Id;
        try {
            org = await _organizationRepository.GetByIdAsync(orgId).AnyContext();
            if (org == null)
                return;

            org.LastEventDateUtc = SystemClock.UtcNow;
            org.SetMonthlyUsage(usage.MonthlyTotal, usage.MonthlyBlocked, usage.MonthlyTooBig);
            if (usage.HourlyBlocked > 0 || usage.HourlyTooBig > 0)
                org.SetHourlyOverage(usage.HourlyTotal, usage.HourlyBlocked, usage.HourlyTooBig, org.GetHourlyEventLimit(usage.MonthlyTotal, usage.MonthlyBlocked, _plans.FreePlan.Id));

            _logger.LogInformation("Saving organization {OrganizationName} usage", org.Name);
            await _organizationRepository.SaveAsync(org, o => o.Cache()).AnyContext();
            await _cache.SetAsync(GetUsageSavedCacheKey(orgId), SystemClock.UtcNow, TimeSpan.FromDays(32)).AnyContext();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error while saving organization {OrganizationId} usage data.", orgId);

            // Set the next document save for 5 seconds in the future.
            await _cache.SetAsync(GetUsageSavedCacheKey(orgId), SystemClock.UtcNow.SubtractMinutes(4).SubtractSeconds(55), TimeSpan.FromDays(32)).AnyContext();
        }
    }

    private async Task SaveUsageAsync(Organization org, Project project, bool justWentOverHourly, bool justWentOverMonthly, Usage usage) {
        bool shouldSaveUsage = await ShouldSaveUsageAsync(org, project, justWentOverHourly, justWentOverMonthly).AnyContext();
        if (!shouldSaveUsage)
            return;

        string projectId = project.Id;
        try {
            project = await _projectRepository.GetByIdAsync(projectId).AnyContext();
            if (project == null)
                return;

            project.LastEventDateUtc = SystemClock.UtcNow;
            project.SetMonthlyUsage(usage.MonthlyTotal, usage.MonthlyBlocked, usage.MonthlyTooBig, org.GetMaxEventsPerMonthWithBonus());
            if (usage.HourlyBlocked > 0 || usage.HourlyTooBig > 0)
                project.SetHourlyOverage(usage.HourlyTotal, usage.HourlyBlocked, usage.HourlyTooBig, org.GetHourlyEventLimit(usage.MonthlyTotal, usage.MonthlyBlocked, _plans.FreePlan.Id));

            _logger.LogInformation("Saving project {ProjectName} usage", project.Name);
            await _projectRepository.SaveAsync(project, o => o.Cache()).AnyContext();
            await _cache.SetAsync(GetUsageSavedCacheKey(org.Id, projectId), SystemClock.UtcNow, TimeSpan.FromDays(32)).AnyContext();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error while saving project {ProjectId} usage data.", projectId);

            // Set the next document save for 5 seconds in the future.
            await _cache.SetAsync(GetUsageSavedCacheKey(org.Id, projectId), SystemClock.UtcNow.SubtractMinutes(4).SubtractSeconds(55), TimeSpan.FromDays(32)).AnyContext();
        }
    }

    private async Task<bool> ShouldSaveUsageAsync(Organization organization, Project project, bool justWentOverHourly, bool justWentOverMonthly) {
        // save usages if we just went over one of the limits
        bool shouldSaveUsage = justWentOverHourly || justWentOverMonthly;
        if (shouldSaveUsage)
            return true;

        var lastCounterSavedDate = await _cache.GetAsync<DateTime>(GetUsageSavedCacheKey(organization.Id, project?.Id)).AnyContext();
        // don't save on the 1st increment, but set the last saved date so we will save in 5 minutes
        if (!lastCounterSavedDate.HasValue)
            await _cache.SetAsync(GetUsageSavedCacheKey(organization.Id, project?.Id), SystemClock.UtcNow, TimeSpan.FromDays(32)).AnyContext();

        // TODO: If the save period is in the next hour we will lose all data in the past five minutes.
        // save usages if the last time we saved them is more than 5 minutes ago
        if (lastCounterSavedDate.HasValue && SystemClock.UtcNow.Subtract(lastCounterSavedDate.Value).TotalMinutes >= 5)
            shouldSaveUsage = true;

        return shouldSaveUsage;
    }

    public async Task<int> GetRemainingEventLimitAsync(Organization organization) {
        if (organization == null || organization.MaxEventsPerMonth < 0)
            return Int32.MaxValue;

        string monthlyCacheKey = GetMonthlyTotalCacheKey(organization.Id);
        int monthlyEventCount = await _cache.GetAsync(monthlyCacheKey, 0).AnyContext();
        return Math.Max(0, organization.GetMaxEventsPerMonthWithBonus() - monthlyEventCount);
    }

    private string GetHourlyBlockedCacheKey(string organizationId, string projectId = null) {
        string key = String.Concat("usage:blocked", ":", SystemClock.UtcNow.ToString("MMddHH"), ":", organizationId);
        return projectId == null ? key : String.Concat(key, ":", projectId);
    }

    private string GetHourlyTotalCacheKey(string organizationId, string projectId = null) {
        string key = String.Concat("usage:total", ":", SystemClock.UtcNow.ToString("MMddHH"), ":", organizationId);
        return projectId == null ? key : String.Concat(key, ":", projectId);
    }

    private string GetHourlyTooBigCacheKey(string organizationId, string projectId = null) {
        string key = String.Concat("usage:toobig", ":", SystemClock.UtcNow.ToString("MMddHH"), ":", organizationId);
        return projectId == null ? key : String.Concat(key, ":", projectId);
    }

    private string GetMonthlyBlockedCacheKey(string organizationId, string projectId = null) {
        string key = String.Concat("usage:blocked", ":", SystemClock.UtcNow.Date.ToString("MM"), ":", organizationId);
        return projectId == null ? key : String.Concat(key, ":", projectId);
    }

    private string GetMonthlyTotalCacheKey(string organizationId, string projectId = null) {
        string key = String.Concat("usage:total", ":", SystemClock.UtcNow.Date.ToString("MM"), ":", organizationId);
        return projectId == null ? key : String.Concat(key, ":", projectId);
    }

    private string GetMonthlyTooBigCacheKey(string organizationId, string projectId = null) {
        string key = String.Concat("usage:toobig", ":", SystemClock.UtcNow.Date.ToString("MM"), ":", organizationId);
        return projectId == null ? key : String.Concat(key, ":", projectId);
    }

    private string GetUsageSavedCacheKey(string organizationId, string projectId = null) {
        string key = String.Concat("usage:saved", ":", organizationId);
        return projectId == null ? key : String.Concat(key, ":", projectId);
    }

    public record struct Usage {
        public int MonthlyTotal { get; set; }
        public int HourlyTotal { get; set; }
        public int MonthlyBlocked { get; set; }
        public int HourlyBlocked { get; set; }
        public int MonthlyTooBig { get; set; }
        public int HourlyTooBig { get; set; }
    }

    public record SaveUsage {
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public DateTime NextSaveUtc { get; set; }
    }
}
