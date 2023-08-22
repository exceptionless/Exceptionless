namespace Exceptionless.Web.Utility.Results;

public record MessageContent
{
    public MessageContent(string message) : this(null, message) { }

    public MessageContent(string id, string message)
    {
        Id = id;
        Message = message;
    }

    public string Id { get; private set; }
    public string Message { get; private set; }
}
