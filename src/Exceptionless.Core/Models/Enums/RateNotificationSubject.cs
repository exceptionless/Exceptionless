using System.Text.Json.Serialization;

namespace Exceptionless.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RateNotificationSubject
{
    Project = 0,
    Stack = 1
}
