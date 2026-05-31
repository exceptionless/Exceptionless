namespace Exceptionless.Core.Models;

public enum RateNotificationSignal
{
    AllEvents = 0,
    Errors = 1,
    CriticalErrors = 2,
    NewErrors = 3,
    Regressions = 4
}
