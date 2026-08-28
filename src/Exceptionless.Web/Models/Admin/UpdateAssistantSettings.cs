using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models.Admin;

public sealed record UpdateAssistantSettings
{
    [MaxLength(200)]
    public string? Model { get; init; }
}
