namespace Exceptionless.Core.Models.WorkItems;

/// <summary>
/// A work item whose progress is tracked by a background task, so the user can follow it in the progress panel.
/// </summary>
public interface IBackgroundTaskWorkItem
{
    /// <summary>The tracking task, or null when the work item was queued without one.</summary>
    string? TaskId { get; }
}
