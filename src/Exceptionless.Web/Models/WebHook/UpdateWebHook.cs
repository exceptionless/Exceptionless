namespace Exceptionless.Web.Models;

public record UpdateWebHook : NewWebHook
{
    public bool IsEnabled { get; set; }
}
