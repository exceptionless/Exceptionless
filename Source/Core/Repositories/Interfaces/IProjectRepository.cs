using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface IProjectRepository : IRepositoryOwnedByOrganization<Project> {
        ICollection<Project> GetByNextSummaryNotificationOffset(byte hourToSendNotificationsAfterUtcMidnight, int limit = 10);
        long IncrementNextSummaryEndOfDayTicks(ICollection<string> ids);
        long GetCountByOrganizationId(string organizationId);
    }
}