namespace Exceptionless.Web.Models;

public record UpdateEvent
{
    public string? EmailAddress { get; set; }
    public string? Description { get; set; }
}
