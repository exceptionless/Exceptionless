using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IProjectRepository : IRepositoryOwnedByOrganization<Project> {
        Task<FindResults<Project>> GetByNextSummaryNotificationOffsetAsync(byte hourToSendNotificationsAfterUtcMidnight, int limit = 50);
        Task IncrementNextSummaryEndOfDayTicksAsync(IReadOnlyCollection<Project> projects);
        Task<CountResult> GetCountByOrganizationIdAsync(string organizationId);
        Task<FindResults<Project>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, CommandOptionsDescriptor<Project> options = null);
    }
}