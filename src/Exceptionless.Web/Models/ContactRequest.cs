using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public record ContactRequest
{
    [Required, StringLength(100)]
    public string? Name { get; init; }

    [Required, EmailAddress, StringLength(254)]
    public string? EmailAddress { get; init; }

    [StringLength(120)]
    public string? Subject { get; init; }

    [StringLength(120)]
    public string? Company { get; init; }

    [Required, StringLength(4000, MinimumLength = 10)]
    public string? Message { get; init; }

    [StringLength(200)]
    public string? Website { get; init; }
}
