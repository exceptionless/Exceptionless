using System;

namespace Exceptionless.Core.Models.Stats {
    public class SessionTermTimelineItem {
        public DateTime Date { get; set; }
        public long Sessions { get; set; }
    }
}