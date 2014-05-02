using System;

namespace Exceptionless.Core.Queues.Models {
    public class WorkItem {
        public string Type { get; set; }
        public byte[] Data { get; set; }
    }
}
