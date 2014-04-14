using System;

namespace Exceptionless.Core.Models {
    public class EventPost {
        public string ContentType { get; set; }
        public string ProjectId { get; set; }
        public byte[] Data { get; set; }
    }
}
