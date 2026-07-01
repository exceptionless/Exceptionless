using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Exceptionless.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum RateNotificationSubject
{
    Project = 0,
    Stack = 1
}
