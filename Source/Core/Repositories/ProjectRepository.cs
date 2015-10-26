using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class ProjectRepository : RepositoryOwnedByOrganization<Project>, IProjectRepository {
        public ProjectRepository(IElasticClient elasticClient, OrganizationIndex index, IValidator<Project> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) 
            : base(elasticClient, index, validator, cacheClient, messagePublisher) {}

        public Task<long> GetCountByOrganizationIdAsync(string organizationId) {
            return CountAsync(new ElasticSearchOptions<Project>().WithOrganizationId(organizationId));
        }

        public Task<FindResults<Project>> GetByNextSummaryNotificationOffsetAsync(byte hourToSendNotificationsAfterUtcMidnight, int limit = 10) {
            var filter = Filter<Project>.Range(r => r.OnField(o => o.NextSummaryEndOfDayTicks).Lower(DateTime.UtcNow.Ticks - (TimeSpan.TicksPerHour * hourToSendNotificationsAfterUtcMidnight)));
            return FindAsync(new ElasticSearchOptions<Project>().WithFilter(filter).WithFields("id", "next_summary_end_of_day_ticks").WithLimit(limit));
        }

        public Task<long> IncrementNextSummaryEndOfDayTicksAsync(ICollection<string> ids) {
            if (ids == null || !ids.Any())
                throw new ArgumentNullException(nameof(ids));

            string script = $"ctx._source.next_summary_end_of_day_ticks += {TimeSpan.TicksPerDay};";
            return UpdateAllAsync((string)null, new QueryOptions().WithIds(ids), script, false);
        }
    }
}