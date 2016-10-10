using System;

namespace Exceptionless.Core.Models.WorkItems {
    public class RemoveOrganizationWorkItem {
        public string OrganizationId { get; set; }
        public bool IsGlobalAdmin { get; set; }
        public string CurrentUserId { get; set; }
    }
}