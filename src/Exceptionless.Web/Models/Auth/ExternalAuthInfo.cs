using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Exceptionless.Web.Models;

// NOTE: This will bypass our LowerCaseUnderscorePropertyNamesContractResolver and provide the correct casing.
public record ExternalAuthInfo
{
    [Required]
    [JsonPropertyName("clientId")]
    public required string ClientId { get; init; }

    [Required]
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [Required]
    [JsonPropertyName("redirectUri")]
    public required string RedirectUri { get; init; }

    [JsonPropertyName("inviteToken")]
    public string? InviteToken { get; init; }
}
