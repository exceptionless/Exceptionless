using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public record Login
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required, StringLength(100, MinimumLength = 6)]
    public required string Password { get; init; }

    [StringLength(40, MinimumLength = 40)]
    public string? InviteToken { get; init; }
}
