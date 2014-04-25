using System;

namespace Exceptionless.Core.Models {
    public class EventPost {
        public string ProjectId { get; set; }
        public byte[] Data { get; set; }
        public string CharSet { get; set; }
        public string MediaType { get; set; }
        public int ApiVersion { get; set; }
        public string UserAgent { get; set; }
    }
}
