using System;

namespace Exceptionless.Api.Models.Project {
    public class ProjectInfoModel {
        public string Id { get; set; }
        public string Name { get; set; }
        public string OrganizationId { get; set; }
        public string OrganizationName { get; set; }
        public double TimeZoneOffset { get; set; }

        public long StackCount { get; set; }
        public long ErrorCount { get; set; }
        public long TotalErrorCount { get; set; }
    }
}