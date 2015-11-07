using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Elasticsearch.Repositories.Queries;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class ProjectRepository : RepositoryOwnedByOrganization<Project>, IProjectRepository {
        public ProjectRepository(ElasticRepositoryContext<Project> context, OrganizationIndex index) : base(context, index) { }

        public Task<long> GetCountByOrganizationIdAsync(string organizationId) {
            return CountAsync(new ExceptionlessQuery().WithOrganizationId(organizationId));
        }

        public Task<FindResults<Project>> GetByNextSummaryNotificationOffsetAsync(byte hourToSendNotificationsAfterUtcMidnight, int limit = 10) {
            var filter = Filter<Project>.Range(r => r.OnField(o => o.NextSummaryEndOfDayTicks).Lower(DateTime.UtcNow.Ticks - (TimeSpan.TicksPerHour * hourToSendNotificationsAfterUtcMidnight)));
            return FindAsync(new ExceptionlessQuery().WithElasticFilter(filter).WithSelectedFields("id", "next_summary_end_of_day_ticks").WithLimit(limit));
        }

        public Task<long> IncrementNextSummaryEndOfDayTicksAsync(ICollection<string> ids) {
            if (ids == null || !ids.Any())
                throw new ArgumentNullException(nameof(ids));

            string script = $"ctx._source.next_summary_end_of_day_ticks += {TimeSpan.TicksPerDay};";
            return UpdateAllAsync((string)null, new ExceptionlessQuery().WithIds(ids), script, false);
        }
    }
}
