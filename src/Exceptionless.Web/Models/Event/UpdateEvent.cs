using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public record UpdateEvent
{
    [EmailAddress]
    public string? EmailAddress { get; set; }
    public string? Description { get; set; }
}
