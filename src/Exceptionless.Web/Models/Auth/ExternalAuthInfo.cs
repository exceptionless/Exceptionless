using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public record ExternalAuthInfo
{
    [Required]
    public required string ClientId { get; init; }
    [Required]
    public required string Code { get; init; }
    [Required]
    public required string RedirectUri { get; init; }
    public string? InviteToken { get; init; }
}
