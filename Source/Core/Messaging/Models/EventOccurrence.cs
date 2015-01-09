using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Messaging.Models {
    public class EventOccurrence {
        public ICollection<string> Ids { get; set; }
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public ICollection<string> StackIds { get; set; }
    }
}
