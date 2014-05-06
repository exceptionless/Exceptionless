using System;
using Exceptionless.Models;

namespace Exceptionless.Api.Models {
    public class ViewProject : NewProject, IIdentity {
        public string Id { get; set; }
        public string OrganizationName { get; set; }
        public double TimeZoneOffset { get; set; }
        public long StackCount { get; set; }
        public long EventCount { get; set; }
        public long TotalEventCount { get; set; }
    }
}