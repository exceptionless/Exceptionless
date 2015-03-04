using System;

namespace Exceptionless.Core.Models {
    public class WebHookEvent {
        public string Id { get; set; }
        public string Url { get { return String.Concat(Settings.Current.BaseURL, "/event/", StackId, "/", Id); } }
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
        public string StackUrl { get { return String.Concat(Settings.Current.BaseURL, "/stack/", StackId); } }
        public string StackTitle { get; set; }
        public string StackDescription { get; set; }
        public TagSet StackTags { get; set; }
        public int TotalOccurrences { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public DateTime? DateFixed { get; set; }
        public bool IsNew { get; set; }
        public bool IsRegression { get; set; }
        public bool IsCritical { get { return Tags != null && Tags.Contains("Critical"); } }
    }
}