using System;

namespace Exceptionless.Core.Models {
    public class WebHookEvent {
        private readonly string _baseUrl;

        public WebHookEvent(string baseUrl) {
            _baseUrl = baseUrl;
        }
        
        public string Id { get; set; }
        public string Url => String.Concat(_baseUrl, "/event/", Id);
        public DateTimeOffset OccurrenceDate { get; set; }
        public TagSet Tags { get; set; }
        public string Type { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string OrganizationId { get; set; }
        public string OrganizationName { get; set; }
        public string StackId { get; set; }
        public string StackUrl => String.Concat(_baseUrl, "/stack/", StackId);
        public string StackTitle { get; set; }
        public string StackDescription { get; set; }
        public TagSet StackTags { get; set; }
        public int TotalOccurrences { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public DateTime? DateFixed { get; set; }
        public bool IsNew { get; set; }
        public bool IsRegression { get; set; }
        public bool IsCritical => Tags != null && Tags.Contains("Critical");
    }
}