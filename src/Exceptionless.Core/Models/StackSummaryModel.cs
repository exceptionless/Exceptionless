using System.Diagnostics;

namespace Exceptionless.Core.Models;

[DebuggerDisplay("Id: {Id}, Status: {Status}, Title: {Title}, First: {FirstOccurrence}, Last: {LastOccurrence}")]
public record StackSummaryModel : SummaryData
{
    public required string Title { get; init; }
    public StackStatus Status { get; init; }
    public DateTime FirstOccurrence { get; init; }
    public DateTime LastOccurrence { get; init; }
    public long Total { get; init; }

    public double Users { get; init; }
    public double TotalUsers { get; init; }
}
