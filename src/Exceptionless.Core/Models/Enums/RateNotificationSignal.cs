using System.Text.Json.Serialization;

namespace Exceptionless.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RateNotificationSignal
{
    AllEvents = 0,
    Errors = 1,
    CriticalErrors = 2,
    NewErrors = 3,
    Regressions = 4
}
