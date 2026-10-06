namespace Exceptionless.Core.Migrations;

public enum MigrationRerunStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

public enum MigrationRerunSource
{
    UserInterface,
    CommandLine
}

public record MigrationRerunOperation
{
    public required string Id { get; init; }
    public required string MigrationId { get; init; }
    public required int Version { get; init; }
    public required MigrationRerunSource Source { get; init; }
    public required MigrationRerunStatus Status { get; set; }
    public string? RequestedByUserId { get; init; }
    public required DateTime RequestedUtc { get; init; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? ErrorMessage { get; set; }
}
