using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IProjectRepository : IRepositoryOwnedByOrganization<Project> {
        Task<IFindResults<Project>> GetByNextSummaryNotificationOffsetAsync(byte hourToSendNotificationsAfterUtcMidnight, int limit = 50);
        Task<CountResult> IncrementNextSummaryEndOfDayTicksAsync(IReadOnlyCollection<Project> projects);
        Task<CountResult> GetCountByOrganizationIdAsync(string organizationId);
        Task<IFindResults<Project>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, PagingOptions paging = null);
    }
}