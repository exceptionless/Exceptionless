namespace Exceptionless.Core.Models.WorkItems;

public record SetLocationFromGeoWorkItem
{
    public required string EventId { get; init; }
    public string? Geo { get; init; }
}
