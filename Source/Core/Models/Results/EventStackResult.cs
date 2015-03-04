using System;

namespace Exceptionless.Core.Models {
    public class EventStackResult : EventResultBase {
        public string Title { get; set; }
        public DateTimeOffset First { get; set; }
        public DateTimeOffset Last { get; set; }
        public long Total { get; set; }
    }
}