using System;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Queues.Models {
    public class EventUserDescription : UserDescription {
        public string ReferenceId { get; set; }
        public string ProjectId { get; set; }
    }
}
