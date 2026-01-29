using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public record Login
{
    /// <summary>
    /// The email address or domain username
    /// </summary>
    [Required]
    public string Email { get; init; } = null!;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; init; } = null!;

    [StringLength(40, MinimumLength = 40)]
    public string? InviteToken { get; init; }
}
