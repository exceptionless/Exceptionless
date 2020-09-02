using System;

namespace Exceptionless.Core.Models.WorkItems
{
    public class RemoveBotEventsWorkItem {
        public string OrganizationId { get; set; }
        public string ClientIpAddress { get; set; }
        public DateTime UtcStartDate { get; set; }
        public DateTime UtcEndDate { get; set; }
    }
}