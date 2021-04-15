using System;
using System.Diagnostics;

namespace Exceptionless.Core.Models {
    [DebuggerDisplay("Id: {Id}, Status: {Status}, Title: {Title}, First: {FirstOccurrence}, Last: {LastOccurrence}")]
    public class StackSummaryModel : SummaryData {
        public string Id { get; set; }
        public string Title { get; set; }
        public StackStatus Status { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public long Total { get; set; }

        public double Users { get; set; }
        public double TotalUsers { get; set; }
    }
}