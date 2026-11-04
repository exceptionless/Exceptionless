using Exceptionless.Core.Models.BackgroundTasks;

namespace Exceptionless.Core.Messaging.Models;

/// <summary>
/// Published after every progress report and every status change of a background task. Intentionally independent of the
/// transport that delivers it to clients, and free of the selection and parameters.
/// </summary>
public record BackgroundTaskUpdated
{
    public required string Id { get; set; }
    public required string OrganizationId { get; set; }
    public string? ProjectId { get; set; }
    public required string Type { get; set; }
    public required string Description { get; set; }
    public BackgroundTaskStatus Status { get; set; }
    public int Progress { get; set; }
    public long Total { get; set; }
    public long Processed { get; set; }
    public long Succeeded { get; set; }
    public long Failed { get; set; }
    public string? Message { get; set; }
    public bool CanCancel { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
