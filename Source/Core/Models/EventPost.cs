using System;

namespace Exceptionless.Core.Models {
    public class EventPost {
        public string ProjectId { get; set; }
        public byte[] Data { get; set; }
        public string ContentEncoding { get; set; }
        public string ContentType { get; set; }
    }
}
