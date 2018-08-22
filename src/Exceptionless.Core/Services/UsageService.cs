using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
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

namespace Exceptionless.Core.Services {
    public sealed class UsageService {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ICacheClient _cache;
        private readonly IMessagePublisher _messagePublisher;
        private readonly ILogger<UsageService> _logger;

        public UsageService(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, ICacheClient cache, IMessagePublisher messagePublisher, ILoggerFactory loggerFactory = null) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _cache = cache;
            _messagePublisher = messagePublisher;
            _logger = loggerFactory.CreateLogger<UsageService>();
        }

        public async Task<bool> IncrementUsageAsync(Organization organization, Project project, bool tooBig, int count = 1, bool applyHourlyLimit = true) {
            if (organization == null || organization.MaxEventsPerMonth < 0 || project == null || count == 0)
                return false;

            var orgUsage = await GetUsageAsync(organization, tooBig, count).AnyContext();
            double totalBlocked = GetTotalBlocked(organization, count, orgUsage, applyHourlyLimit);
            bool overLimit = totalBlocked > 0;
            if (overLimit) {
                orgUsage.HourlyBlocked = await _cache.IncrementAsync(GetHourlyBlockedCacheKey(organization.Id), (int)totalBlocked, TimeSpan.FromMinutes(61), (uint)orgUsage.HourlyBlocked).AnyContext();
                orgUsage.MonthlyBlocked = await _cache.IncrementAsync(GetMonthlyBlockedCacheKey(organization.Id), (int)totalBlocked, TimeSpan.FromDays(32), (uint)orgUsage.MonthlyBlocked).AnyContext();
            }

            bool justWentOverHourly = orgUsage.HourlyTotal > organization.GetHourlyEventLimit() && orgUsage.HourlyTotal <= organization.GetHourlyEventLimit() + count;
            bool justWentOverMonthly = orgUsage.MonthlyTotal > organization.GetMaxEventsPerMonthWithBonus() && orgUsage.MonthlyTotal <= organization.GetMaxEventsPerMonthWithBonus() + count;
            var projectUsage = await GetUsageAsync(organization, project, tooBig, count, overLimit, (int)totalBlocked).AnyContext();

            var tasks = new List<Task>(3) {
                SaveUsageAsync(organization, justWentOverHourly, justWentOverMonthly, orgUsage),
                SaveUsageAsync(organization, project, justWentOverHourly, justWentOverMonthly, projectUsage)
            };

            if (justWentOverMonthly)
                tasks.Add(_messagePublisher.PublishAsync(new PlanOverage { OrganizationId = organization.Id }));
            else if (justWentOverHourly)
                tasks.Add(_messagePublisher.PublishAsync(new PlanOverage { OrganizationId = organization.Id, IsHourly = true }));

            await Task.WhenAll(tasks).AnyContext();
            return overLimit;
        }

        private async Task<Usage> GetUsageAsync(Organization org, bool tooBig, int count) {
            var hourlyTotal = _cache.IncrementAsync(GetHourlyTotalCacheKey(org.Id), count, TimeSpan.FromMinutes(61), (uint)org.GetCurrentHourlyTotal());
            var monthlyTotal = _cache.IncrementAsync(GetMonthlyTotalCacheKey(org.Id), count, TimeSpan.FromDays(32), (uint)org.GetCurrentMonthlyTotal());
            var hourlyTooBig = _cache.IncrementIfAsync(GetHourlyTooBigCacheKey(org.Id), count, TimeSpan.FromMinutes(61), tooBig, (uint)org.GetCurrentHourlyTooBig());
            var monthlyTooBig = _cache.IncrementIfAsync(GetMonthlyTooBigCacheKey(org.Id), count, TimeSpan.FromDays(32), tooBig, (uint)org.GetCurrentMonthlyTooBig());
            var hourlyBlocked = _cache.GetAsync<long>(GetHourlyBlockedCacheKey(org.Id), org.GetCurrentHourlyBlocked());
            var monthlyBlocked = _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(org.Id), org.GetCurrentMonthlyBlocked());
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

        private async Task<Usage> GetUsageAsync(Organization org, Project project, bool tooBig, int count, bool overLimit, int totalBlocked) {
            var hourlyTotal = _cache.IncrementAsync(GetHourlyTotalCacheKey(org.Id, project.Id), count, TimeSpan.FromMinutes(61), (uint)project.GetCurrentHourlyTotal());
            var monthlyTotal = _cache.IncrementAsync(GetMonthlyTotalCacheKey(org.Id, project.Id), count, TimeSpan.FromDays(32), (uint)project.GetCurrentMonthlyTotal());
            var hourlyTooBig = _cache.IncrementIfAsync(GetHourlyTooBigCacheKey(org.Id, project.Id), count, TimeSpan.FromMinutes(61), tooBig, (uint)project.GetCurrentHourlyTooBig());
            var monthlyTooBig = _cache.IncrementIfAsync(GetMonthlyTooBigCacheKey(org.Id, project.Id), count, TimeSpan.FromDays(32), tooBig, (uint)project.GetCurrentMonthlyTooBig());
            var hourlyBlocked = _cache.IncrementIfAsync(GetHourlyBlockedCacheKey(org.Id, project.Id), totalBlocked, TimeSpan.FromMinutes(61), overLimit, (uint)project.GetCurrentHourlyBlocked());
            var monthlyBlocked = _cache.IncrementIfAsync(GetMonthlyBlockedCacheKey(org.Id, project.Id), totalBlocked, TimeSpan.FromDays(32), overLimit, (uint)project.GetCurrentMonthlyBlocked());
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

            try {
                org = await _organizationRepository.GetByIdAsync(org.Id).AnyContext();
                org.SetMonthlyUsage(usage.MonthlyTotal, usage.MonthlyBlocked, usage.MonthlyTooBig);
                if (usage.HourlyBlocked > 0 || usage.HourlyTooBig > 0)
                    org.SetHourlyOverage(usage.HourlyTotal, usage.HourlyBlocked, usage.HourlyTooBig);

                await _organizationRepository.SaveAsync(org, o => o.Cache()).AnyContext();
                await _cache.SetAsync(GetUsageSavedCacheKey(org.Id), SystemClock.UtcNow, TimeSpan.FromDays(32)).AnyContext();
            } catch (Exception ex) {
                _logger.LogError(ex, "Error while saving organization usage data.");

                // Set the next document save for 5 seconds in the future.
                await _cache.SetAsync(GetUsageSavedCacheKey(org.Id), SystemClock.UtcNow.SubtractMinutes(4).SubtractSeconds(55), TimeSpan.FromDays(32)).AnyContext();
            }
        }

        private async Task SaveUsageAsync(Organization org, Project project, bool justWentOverHourly, bool justWentOverMonthly, Usage usage) {
            bool shouldSaveUsage = await ShouldSaveUsageAsync(org, project, justWentOverHourly, justWentOverMonthly).AnyContext();
            if (!shouldSaveUsage)
                return;

            try {
                project = await _projectRepository.GetByIdAsync(project.Id).AnyContext();
                project.SetMonthlyUsage(usage.MonthlyTotal, usage.MonthlyBlocked, usage.MonthlyTooBig, org.GetMaxEventsPerMonthWithBonus());
                if (usage.HourlyBlocked > 0 || usage.HourlyTooBig > 0)
                    project.SetHourlyOverage(usage.HourlyTotal, usage.HourlyBlocked, usage.HourlyTooBig, org.GetHourlyEventLimit());

                await _projectRepository.SaveAsync(project, o => o.Cache()).AnyContext();
                await _cache.SetAsync(GetUsageSavedCacheKey(org.Id, project.Id), SystemClock.UtcNow, TimeSpan.FromDays(32)).AnyContext();
            } catch (Exception ex) {
                _logger.LogError(ex, "Error while saving project usage data.");

                // Set the next document save for 5 seconds in the future.
                await _cache.SetAsync(GetUsageSavedCacheKey(org.Id, project.Id), SystemClock.UtcNow.SubtractMinutes(4).SubtractSeconds(55), TimeSpan.FromDays(32)).AnyContext();
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
            long monthlyEventCount = await _cache.GetAsync<long>(monthlyCacheKey, 0).AnyContext();
            return Math.Max(0, organization.GetMaxEventsPerMonthWithBonus() - (int)monthlyEventCount);
        }

        private double GetTotalBlocked(Organization organization, int count, Usage usage, bool applyHourlyLimit) {
            if (organization.IsSuspended)
                return count;

            int hourlyEventLimit = organization.GetHourlyEventLimit();
            int monthlyEventLimit = organization.GetMaxEventsPerMonthWithBonus();
            double originalAllowedMonthlyEventTotal = usage.MonthlyTotal - usage.MonthlyBlocked - count;

            // If the original count is less than the max events per month and original count + hourly limit is greater than the max events per month then use the monthly limit.
            if (originalAllowedMonthlyEventTotal < monthlyEventLimit && (originalAllowedMonthlyEventTotal + hourlyEventLimit) >= monthlyEventLimit)
                return originalAllowedMonthlyEventTotal < monthlyEventLimit ? usage.MonthlyTotal - usage.MonthlyBlocked - monthlyEventLimit : count;

            double originalAllowedHourlyEventTotal = usage.HourlyTotal - usage.HourlyBlocked - count;
            if (applyHourlyLimit && (usage.HourlyTotal - usage.HourlyBlocked) > hourlyEventLimit)
                return originalAllowedHourlyEventTotal < hourlyEventLimit ? usage.HourlyTotal - usage.HourlyBlocked - hourlyEventLimit : count;

            if ((usage.MonthlyTotal - usage.MonthlyBlocked) > monthlyEventLimit)
                return originalAllowedMonthlyEventTotal < monthlyEventLimit ? usage.MonthlyTotal - usage.MonthlyBlocked - monthlyEventLimit : count;

            return 0;
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

        [DebuggerDisplay("MonthlyTotal: {MonthlyTotal}, HourlyTotal: {HourlyTotal}, MonthlyBlocked: {MonthlyBlocked}, HourlyBlocked: {HourlyBlocked}, MonthlyTooBig: {MonthlyTooBig}, HourlyTooBig: {HourlyTooBig}")]
        private struct Usage {
            public double MonthlyTotal { get; set; }
            public double HourlyTotal { get; set; }
            public double MonthlyBlocked { get; set; }
            public double HourlyBlocked { get; set; }
            public double MonthlyTooBig { get; set; }
            public double HourlyTooBig { get; set; }
        }
    }
}
