using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Exceptionless.Core.Models {
    [DebuggerDisplay("Id: {Id}, Name: {Name}, NextSummaryEndOfDayTicks: {NextSummaryEndOfDayTicks}")]
    public class Project : IOwnedByOrganizationWithIdentity, IData {
        public Project() {
            Configuration = new ClientConfiguration();
            NotificationSettings = new Dictionary<string, NotificationSettings>();
            PromotedTabs = new HashSet<string>();
            DeleteBotDataEnabled = true;
            Data = new DataDictionary();
        }

        /// <summary>
        /// Unique id that identifies an project.
        /// </summary>
        public string Id { get; set; }

        public string OrganizationId { get; set; }

        public string Name { get; set; }

        public ClientConfiguration Configuration { get; set; }

        public Dictionary<string, NotificationSettings> NotificationSettings { get; set; }

        /// <summary>
        /// Optional data entries that contain additional configuration information for this project.
        /// </summary>
        public DataDictionary Data { get; set; }

        public HashSet<string> PromotedTabs { get; set; }

        public string CustomContent { get; set; }

        public bool DeleteBotDataEnabled { get; set; }

        /// <summary>
        /// The tick count that represents the next time the daily summary job should run. This time is set to midnight of the
        /// projects local time.
        /// </summary>
        public Int64 NextSummaryEndOfDayTicks { get; set; }
    }
}