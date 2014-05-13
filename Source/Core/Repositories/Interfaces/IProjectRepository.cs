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
        Project GetByApiKey(string apiKey);
        IList<Project> GetByNextSummaryNotificationOffset(byte hourToSendNotificationsAfterUtcMidnight, int limit = 10);
        long IncrementNextSummaryEndOfDayTicks(IList<string> ids);
        IList<TimeSpan> GetTargetTimeOffsetsForStats(string projectId);
        TimeSpan GetDefaultTimeOffset(string projectId);
        TimeZoneInfo GetDefaultTimeZone(string projectId);
        DateTime UtcToDefaultProjectLocalTime(string id, DateTime utcDateTime);
        DateTimeOffset UtcToDefaultProjectLocalTime(string id, DateTimeOffset dateTimeOffset);
        DateTime DefaultProjectLocalTimeToUtc(string id, DateTime dateTime);

        void IncrementStats(string projectId, long? eventCount = null, long? stackCount = null);
    }
}