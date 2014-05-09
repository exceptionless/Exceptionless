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

        /// <summary>
        /// Gets a list of the time offsets that should be used for creating localized stats documents.
        /// </summary>
        /// <param name="projectId">The target project id.</param>
        /// <returns></returns>
        IEnumerable<TimeSpan> GetTargetTimeOffsetsForStats(string projectId);

        /// <summary>
        /// Gets the default time offset for the project.
        /// </summary>
        /// <param name="projectId">The target project id.</param>
        /// <returns></returns>
        TimeSpan GetDefaultTimeOffset(string projectId);

        /// <summary>
        /// Gets the default time zone for the project.
        /// </summary>
        /// <param name="projectId">The target project id.</param>
        /// <returns></returns>
        TimeZoneInfo GetDefaultTimeZone(string projectId);

        /// <summary>
        /// Converts the DateTime that is in UTC to the project's default time offset.
        /// </summary>
        /// <param name="projectId">The target project id.</param>
        /// <param name="utcDateTime">The UTC date to convert.</param>
        /// <returns></returns>
        DateTime UtcToDefaultProjectLocalTime(string projectId, DateTime utcDateTime);

        /// <summary>
        /// Convert a UTC DateTime to the project's default time offset.
        /// </summary>
        /// <param name="projectId">The project id to get the default time offset from.</param>
        /// <param name="dateTimeOffset"></param>
        /// <returns></returns>
        DateTimeOffset UtcToDefaultProjectLocalTime(string projectId, DateTimeOffset dateTimeOffset);

        /// <summary>
        /// Converts a DateTime that is in the project's default local time to UTC.
        /// </summary>
        /// <param name="projectId">The project id to get the default time offset from.</param>
        /// <param name="dateTime">The DateTime that is in the project's default time offset.</param>
        /// <returns></returns>
        DateTime DefaultProjectLocalTimeToUtc(string projectId, DateTime dateTime);
    }
}