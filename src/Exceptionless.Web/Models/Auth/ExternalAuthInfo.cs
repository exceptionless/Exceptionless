using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Exceptionless.Web.Models;

public record ExternalAuthInfo
{
    [Required]
    [JsonProperty("clientId")]
    public required string ClientId { get; init; }

    [Required]
    [JsonProperty("code")]
    public required string Code { get; init; }

    [Required]
    [JsonProperty("redirectUri")]
    public required string RedirectUri { get; init; }

    [JsonProperty("inviteToken")]
    public string? InviteToken { get; init; }
}
