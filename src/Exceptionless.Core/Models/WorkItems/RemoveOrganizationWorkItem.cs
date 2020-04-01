namespace Exceptionless.Core.Models.WorkItems {
    public class RemoveOrganizationWorkItem {
        public string OrganizationId { get; set; }
        public string CurrentUserId { get; set; }
    }
}