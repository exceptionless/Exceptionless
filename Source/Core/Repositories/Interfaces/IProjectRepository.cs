#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless.Core.Repositories {
    public interface IProjectRepository : IRepositoryOwnedByOrganization<Project> {
        ICollection<Project> GetByNextSummaryNotificationOffset(byte hourToSendNotificationsAfterUtcMidnight, int limit = 10);
        long IncrementNextSummaryEndOfDayTicks(ICollection<string> ids);
        void IncrementEventCounter(string projectId, long eventCount = 1);
        long GetCountByOrganizationId(string organizationId);
    }
}