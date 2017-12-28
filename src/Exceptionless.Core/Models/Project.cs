using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models {
    [DebuggerDisplay("Id: {Id}, Name: {Name}, NextSummaryEndOfDayTicks: {NextSummaryEndOfDayTicks}")]
    public class Project : IOwnedByOrganizationWithIdentity, IData, IHaveDates {
        public Project() {
            Configuration = new ClientConfiguration();
            NotificationSettings = new Dictionary<string, NotificationSettings>();
            PromotedTabs = new HashSet<string>();
            DeleteBotDataEnabled = false;
            Usage = new Collection<UsageInfo>();
            OverageHours = new Collection<UsageInfo>();
            Data = new DataDictionary();
        }

        /// <summary>
        /// Unique id that identifies an project.
        /// </summary>
        public string Id { get; set; }

        public string OrganizationId { get; set; }

        public string Name { get; set; }

        /// <summary>
        /// Returns true if we've detected that the project has received data.
        /// </summary>
        public bool? IsConfigured { get; set; }

        public ClientConfiguration Configuration { get; set; }

        public Dictionary<string, NotificationSettings> NotificationSettings { get; set; }

        /// <summary>
        /// Hours over event limit.
        /// </summary>
        public ICollection<UsageInfo> OverageHours { get; set; }

        /// <summary>
        /// Account event usage information.
        /// </summary>
        public ICollection<UsageInfo> Usage { get; set; }

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
        public long NextSummaryEndOfDayTicks { get; set; }

        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }

        public static class NotificationIntegrations {
            public const string Slack = "slack";
        }

        public static class KnownDataKeys {
            public const string SlackToken = "-@slack";
        }
    }
}
