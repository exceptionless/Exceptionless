using System;

namespace Exceptionless.Api.Models {
    public class NewProject {   
        public string OrganizationId { get; set; }
        public string Name { get; set; }
        public string TimeZone { get; set; }
        public string CustomContent { get; set; }
    }
}