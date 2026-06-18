using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Exceptionless.Web.Models;

// NOTE: Explicit [JsonPropertyName] attributes ensure camelCase keys for these properties,
// overriding the global SnakeCaseLower naming policy.
public record ExternalAuthInfo
{
    [Required]
    [JsonPropertyName("clientId")]
    public string ClientId { get; init; } = null!;

    [Required]
    [JsonPropertyName("code")]
    public string Code { get; init; } = null!;

    [Required]
    [Url]
    [JsonPropertyName("redirectUri")]
    public string RedirectUri { get; init; } = null!;

    [JsonPropertyName("inviteToken")]
    public string? InviteToken { get; init; }
}
