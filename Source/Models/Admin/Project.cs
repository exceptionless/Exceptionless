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
using System.Diagnostics;
using Exceptionless.Models;

namespace Exceptionless.Models {
    [DebuggerDisplay("Id: {Id}, Name: {Name}, NextSummaryEndOfDayTicks: {NextSummaryEndOfDayTicks}")]
    public class Project : IOwnedByOrganization, IData {
        public Project() {
            ApiKeys = new HashSet<string>();
            Configuration = new ClientConfiguration();
            NotificationSettings = new Dictionary<string, NotificationSettings>();
            PromotedTabs = new HashSet<string>();
            Data = new DataDictionary();
        }

        /// <summary>
        /// Unique id that identifies an project.
        /// </summary>
        public string Id { get; set; }

        public string OrganizationId { get; set; }

        public string Name { get; set; }

        public string TimeZone { get; set; }

        public HashSet<string> ApiKeys { get; set; }

        public ClientConfiguration Configuration { get; set; }

        public Dictionary<string, NotificationSettings> NotificationSettings { get; set; }

        /// <summary>
        /// Optional data entries that contain additional configuration information for this project.
        /// </summary>
        public DataDictionary Data { get; set; }

        public HashSet<string> PromotedTabs { get; set; }

        public string CustomContent { get; set; }

        /// <summary>
        /// Current number of error stacks in the system.
        /// </summary>
        public long StackCount { get; set; }

        /// <summary>
        /// Current number of error occurrences in the system.
        /// </summary>
        public long ErrorCount { get; set; }

        /// <summary>
        /// Total errors logged by our system.
        /// </summary>
        public long TotalErrorCount { get; set; }

        /// <summary>
        /// The date that the latest error occurred.
        /// </summary>
        public DateTime LastErrorDate { get; set; }

        /// <summary>
        /// The tick count that represents the next time the daily summary job should run. This time is set to midnight of the
        /// projects local time.
        /// </summary>
        public Int64 NextSummaryEndOfDayTicks { get; set; }
    }
}