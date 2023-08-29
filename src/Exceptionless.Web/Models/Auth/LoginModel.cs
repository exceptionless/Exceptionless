using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace Exceptionless.Web.Models;

public record LoginModel
{
    [Required, EmailAddress]
    public string Email { get; init; } = null!;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; init; } = null!;

    [StringLength(40, MinimumLength = 40)]
    public string? InviteToken { get; init; }
}
