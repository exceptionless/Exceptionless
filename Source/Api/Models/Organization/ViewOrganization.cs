using System;
using Exceptionless.Models;

namespace Exceptionless.Api.Models {
    public class ViewOrganization : IIdentity {
        public string Id { get; set; }
        public string Name { get; set; }
        public string PlanId { get; set; }
        public int ProjectCount { get; set; }
        public long StackCount { get; set; }
        public long TotalEventCount { get; set; }
    }
}