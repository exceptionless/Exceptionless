using System;

namespace Exceptionless.Core.Models {
    public class EventPost {
        public string ProjectId { get; set; }
        public byte[] Data { get; set; }
        public string CharSet { get; set; }
        public string MediaType { get; set; }
        public int ApiVersion { get; set; }
        public string UserAgent { get; set; }
        public string ContentEncoding { get; set; }
        public string IpAddress { get; set; }
    }

    public class EventPostFileInfo {
        public EventPostFileInfo() {
            ShouldArchive = true;
        }

        public bool ShouldArchive { get; set; }
        public string FilePath { get; set; }
    }
}
