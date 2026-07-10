using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models.Admin;

public record NewOAuthApplication
{
    [Required]
    [MaxLength(2048)]
    public required string ClientId { get; init; }

    [Required]
    [MaxLength(200)]
    public required string Name { get; init; }

    [Required]
    [Length(1, 20)]
    public required string[] RedirectUris { get; init; }

    [Required]
    [Length(1, 20)]
    public required string[] Scopes { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    public bool IsDisabled { get; init; }
}
