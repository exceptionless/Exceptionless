using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class ProjectRepository : ElasticSearchRepositoryOwnedByOrganization<Project>, IProjectRepository {
        public ProjectRepository(IElasticClient elasticClient, OrganizationIndex index, IValidator<Project> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) 
            : base(elasticClient, index, validator, cacheClient, messagePublisher) {}

        public long GetCountByOrganizationId(string organizationId) {
            return Count(new ElasticSearchOptions<Project>().WithOrganizationId(organizationId));
        }

        public FindResults<Project> GetByNextSummaryNotificationOffset(byte hourToSendNotificationsAfterUtcMidnight, int limit = 10) {
            var filter = Filter<Project>.Range(r => r.OnField(o => o.NextSummaryEndOfDayTicks).Lower(DateTime.UtcNow.Ticks - (TimeSpan.TicksPerHour * hourToSendNotificationsAfterUtcMidnight)));
            return Find(new ElasticSearchOptions<Project>().WithFilter(filter).WithFields(FieldNames.Id, FieldNames.NextSummaryEndOfDayTicks).WithLimit(limit));
        }

        public long IncrementNextSummaryEndOfDayTicks(ICollection<string> ids) {
            if (ids == null || !ids.Any())
                throw new ArgumentNullException("ids");

            string script = String.Format("ctx._source.next_summary_end_of_day_ticks += {0};", TimeSpan.TicksPerDay);
            return UpdateAll((string)null, new QueryOptions().WithProjectIds(ids), script, false);
        }

        //private static class FieldNames {
        //    public const string Id = CommonFieldNames.Id;
        //    public const string OrganizationId = CommonFieldNames.OrganizationId;
        //    public const string Name = "Name";
        //    public const string Configuration = "Configuration";
        //    public const string Configuration_Version = "Configuration.Version";
        //    public const string NotificationSettings = "NotificationSettings";
        //    public const string PromotedTabs = "PromotedTabs";
        //    public const string CustomContent = "CustomContent";
        //    public const string TotalEventCount = "TotalEventCount";
        //    public const string LastEventDate = "LastEventDate";
        //    public const string NextSummaryEndOfDayTicks = "NextSummaryEndOfDayTicks";
        //}
    }
}