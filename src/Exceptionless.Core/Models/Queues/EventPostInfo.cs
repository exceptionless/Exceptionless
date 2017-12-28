using System;

namespace Exceptionless.Core.Queues.Models {
    public class EventPostInfo {
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string CharSet { get; set; }
        public string MediaType { get; set; }
        public int ApiVersion { get; set; }
        public string UserAgent { get; set; }
        public string ContentEncoding { get; set; }
        public string IpAddress { get; set; }
    }

    public class EventPost : EventPostInfo {
        public EventPost() {
            ShouldArchive = Settings.Current.EnableArchive;
        }

        public bool ShouldArchive { get; set; }
        public string FilePath { get; set; }
    }
}
