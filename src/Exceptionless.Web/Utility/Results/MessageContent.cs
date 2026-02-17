using System.Text.Json.Serialization;

namespace Exceptionless.Web.Utility.Results;

[method: JsonConstructor]
public record MessageContent(string? Id, string Message)
{
    public MessageContent(string message) : this(null, message)
    {
    }
}
