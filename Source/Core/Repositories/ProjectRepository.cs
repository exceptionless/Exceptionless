using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Repositories.Elasticsearch.Queries;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
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

            return CountAsync(new ExceptionlessQuery()
                .WithOrganizationId(organizationId)
                .WithCacheKey(String.Concat("Organization:", organizationId)));
        }

        public Task<FindResults<Project>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, PagingOptions paging = null) {
            if (organizationIds == null)
                throw new ArgumentNullException(nameof(organizationIds));

            if (organizationIds.Count == 0)
                return Task.FromResult<FindResults<Project>>(new FindResults<Project>());

            string cacheKey = organizationIds.Count == 1 ?  String.Concat("paged:Organization:", organizationIds.Single()) : null;
            return FindAsync(new ExceptionlessQuery()
                .WithOrganizationIds(organizationIds)
                .WithPaging(paging)
                .WithCacheKey(cacheKey));
        }

        public Task<FindResults<Project>> GetByNextSummaryNotificationOffsetAsync(byte hourToSendNotificationsAfterUtcMidnight, int limit = 50) {
            var filter = Filter<Project>.Range(r => r.OnField(o => o.NextSummaryEndOfDayTicks).Lower(SystemClock.UtcNow.Ticks - (TimeSpan.TicksPerHour * hourToSendNotificationsAfterUtcMidnight)));
            return FindAsync(new ExceptionlessQuery().WithElasticFilter(filter).WithLimit(limit).WithSort(EventIndexType.Fields.OrganizationId));
        }

        public async Task IncrementNextSummaryEndOfDayTicksAsync(IReadOnlyCollection<Project> projects) {
            if (projects == null)
                throw new ArgumentNullException(nameof(projects));

            if (projects.Count == 0)
                return;

            string script = $"ctx._source.next_summary_end_of_day_ticks += {TimeSpan.TicksPerDay};";
            await PatchAsync(projects.Select(p => p.Id), script, false).AnyContext();
            await InvalidateCacheAsync(projects).AnyContext();
        }

        protected override async Task InvalidateCachedQueriesAsync(IReadOnlyCollection<Project> documents) {
            var organizations = documents.Select(d => d.OrganizationId).Distinct().Where(id => !String.IsNullOrEmpty(id));
            await Cache.RemoveAllAsync(organizations.Select(id => $"count:Organization:{id}")).AnyContext();
            await base.InvalidateCachedQueriesAsync(documents).AnyContext();
        }
    }
}
