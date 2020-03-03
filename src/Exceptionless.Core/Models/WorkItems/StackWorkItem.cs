namespace Exceptionless.Core.Models.WorkItems {
    public class StackWorkItem {
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string StackId { get; set; }
        public bool Delete { get; set; }
    }
}