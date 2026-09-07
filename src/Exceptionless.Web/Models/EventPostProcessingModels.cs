using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public sealed record EventPostProcessingStatusRequest
{
    [Required, MinLength(1), MaxLength(1000)]
    public required IReadOnlyCollection<string> Ids { get; init; }
}

public sealed record EventPostProcessingSummary(
    int Requested,
    int Queued,
    int Completed,
    int Unknown);

public sealed record EventIngestionV3ProcessingStatusRequest
{
    [Required, MinLength(1), MaxLength(1000)]
    public required IReadOnlyCollection<string> ClientIds { get; init; }
}

public sealed record EventIngestionV3ProcessingSummary(
    int Requested,
    int Pending,
    int Completed);
