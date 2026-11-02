namespace Exceptionless.Core.Models;

/// <summary>
/// A notification that waits for the first occurrence in a notifying environment.
/// </summary>
public enum PendingStackNotification
{
    New,
    Regression
}
