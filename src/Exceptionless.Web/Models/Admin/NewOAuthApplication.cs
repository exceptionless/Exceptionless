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

    [Length(0, 20)]
    public string[]? RedirectUris { get; init; }

    [Required]
    [Length(1, 20)]
    public required string[] Scopes { get; init; }

    [Length(1, 3)]
    public string[]? GrantTypes { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    public bool IsDisabled { get; init; }
}
