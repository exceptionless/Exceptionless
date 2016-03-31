﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Elasticsearch.Repositories.Queries;
using Foundatio.Logging;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class ProjectRepository : RepositoryOwnedByOrganization<Project>, IProjectRepository {
        public ProjectRepository(ElasticRepositoryContext<Project> context, OrganizationIndex index, ILoggerFactory loggerFactory = null) : base(context, index, loggerFactory) {
            DocumentsAdded.AddHandler(OnDocumentsAdded);
        }
        
        public Task<long> GetCountByOrganizationIdAsync(string organizationId) {
            return CountAsync(new ExceptionlessQuery().WithOrganizationId(organizationId).WithCacheKey(organizationId));
        }

        public Task<FindResults<Project>> GetByNextSummaryNotificationOffsetAsync(byte hourToSendNotificationsAfterUtcMidnight, int limit = 10) {
            var filter = Query<Project>.Range(r => r.Field(o => o.NextSummaryEndOfDayTicks).LessThan(DateTime.UtcNow.Ticks - (TimeSpan.TicksPerHour * hourToSendNotificationsAfterUtcMidnight)));
            return FindAsync(new ExceptionlessQuery().WithElasticFilter(filter).WithSelectedFields("id", "next_summary_end_of_day_ticks").WithLimit(limit));
        }

        public async Task<long> IncrementNextSummaryEndOfDayTicksAsync(ICollection<Project> projects) {
            if (projects == null || !projects.Any())
                throw new ArgumentNullException(nameof(projects));
            
            string script = $"ctx._source.next_summary_end_of_day_ticks += {TimeSpan.TicksPerDay};";
            var recordsAffected = await UpdateAllAsync((string)null, new ExceptionlessQuery().WithIds(projects.Select(p => p.Id)), script, false).AnyContext();
            if (recordsAffected > 0)
                await InvalidateCacheAsync(projects).AnyContext();
            
            return recordsAffected;
        }

        protected override async Task InvalidateCacheAsync(ICollection<ModifiedDocument<Project>> documents) {
            if (!IsCacheEnabled)
                return;
            
            await InvalidateCountCacheAsync(documents.Select(d => d.Value.OrganizationId)).AnyContext();
            await base.InvalidateCacheAsync(documents).AnyContext();
        }
        
        private async Task OnDocumentsAdded(object sender, DocumentsEventArgs<Project> documents) {
            if (!IsCacheEnabled)
                return;

            await InvalidateCountCacheAsync(documents.Documents.Select(d => d.OrganizationId)).AnyContext();
        }

        private async Task InvalidateCountCacheAsync(IEnumerable<string> organizationIds) {
            var keys = organizationIds.Where(id => !String.IsNullOrEmpty(id)).Select(id => $"count:{id}").Distinct().ToList();
            if (keys.Count > 0)
                await Cache.RemoveAllAsync(keys).AnyContext();
        }
    }
}
