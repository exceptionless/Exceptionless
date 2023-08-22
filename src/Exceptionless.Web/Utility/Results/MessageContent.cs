namespace Exceptionless.Web.Utility.Results;

public record MessageContent(string? Id, string Message)
{
    public MessageContent(string message) : this(null, message)
    {
    }

    public string? Id { get; private set; } = Id;
    public string Message { get; private set; } = Message;
}
