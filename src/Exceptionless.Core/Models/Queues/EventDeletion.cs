namespace Exceptionless.Core.Queues.Models {
    public class EventDeletion {
        public string[] OrganizationIds { get; set; }
        public string[] ProjectIds { get; set; }
        public string[] StackIds { get; set; }
        public string[] EventIds { get; set; }
    }
}