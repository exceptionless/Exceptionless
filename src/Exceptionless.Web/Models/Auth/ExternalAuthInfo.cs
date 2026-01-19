using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Exceptionless.Web.Models;

// NOTE: This will bypass our LowerCaseUnderscorePropertyNamesContractResolver and provide the correct casing.
public record ExternalAuthInfo
{
    [Required]
    [JsonPropertyName("clientId")]
    public string ClientId { get; init; } = null!;

    [Required]
    [JsonPropertyName("code")]
    public string Code { get; init; } = null!;

    [Required]
    [JsonPropertyName("redirectUri")]
    public string RedirectUri { get; init; } = null!;

    [JsonPropertyName("inviteToken")]
    public string? InviteToken { get; init; }
}
