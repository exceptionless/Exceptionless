using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Exceptionless.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum RateNotificationSignal
{
    AllEvents = 0,
    Errors = 1,
    CriticalErrors = 2,
    NewErrors = 3,
    Regressions = 4
}
