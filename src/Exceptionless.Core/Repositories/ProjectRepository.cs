using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class ProjectRepository : RepositoryOwnedByOrganization<Project>, IProjectRepository {
        public ProjectRepository(ExceptionlessElasticConfiguration configuration, IValidator<Project> validator)
            : base(configuration.Organizations.Project, validator) {
        }

        public Task<CountResult> GetCountByOrganizationIdAsync(string organizationId) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));

            return CountAsync(q => q.Organization(organizationId), o => o.CacheKey(String.Concat("Organization:", organizationId)));
        }

        public Task<FindResults<Project>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, CommandOptionsDescriptor<Project> options = null) {
            if (organizationIds == null)
                throw new ArgumentNullException(nameof(organizationIds));

            if (organizationIds.Count == 0)
                return Task.FromResult(new FindResults<Project>());

            return FindAsync(q => q.Organizations(organizationIds), options);
        }

        public Task<FindResults<Project>> GetByNextSummaryNotificationOffsetAsync(byte hourToSendNotificationsAfterUtcMidnight, int limit = 50) {
            var filter = Query<Project>.Range(r => r.Field(o => o.NextSummaryEndOfDayTicks).LessThan(SystemClock.UtcNow.Ticks - (TimeSpan.TicksPerHour * hourToSendNotificationsAfterUtcMidnight)));
            return FindAsync(q => q.ElasticFilter(filter).SortAscending(p => p.OrganizationId), o => o.SnapshotPaging().PageLimit(limit));
        }

        public async Task IncrementNextSummaryEndOfDayTicksAsync(IReadOnlyCollection<Project> projects) {
            if (projects == null)
                throw new ArgumentNullException(nameof(projects));

            if (projects.Count == 0)
                return;

            string script = $"ctx._source.next_summary_end_of_day_ticks += {TimeSpan.TicksPerDay}L;";
            await this.PatchAsync(projects.Select(p => p.Id).ToArray(), new ScriptPatch(script), o => o.Notifications(false)).AnyContext();
            await InvalidateCacheAsync(projects).AnyContext();
        }

        protected override Task InvalidateCachedQueriesAsync(IReadOnlyCollection<Project> documents, ICommandOptions options = null) {
            var organizations = documents.Select(d => d.OrganizationId).Distinct().Where(id => !String.IsNullOrEmpty(id));
            return Task.WhenAll(Cache.RemoveAllAsync(organizations.Select(id => $"count:Organization:{id}")), base.InvalidateCachedQueriesAsync(documents, options));
        }
    }
}
