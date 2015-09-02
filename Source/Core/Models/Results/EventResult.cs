using System;

namespace Exceptionless.Core.Models {
    public class EventResult : EventResultBase {
        public string Message { get; set; }
        public DateTimeOffset Date { get; set; }
    }
}