using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Exceptionless.Web.Models;

// NOTE: This will bypass our LowerCaseUnderscorePropertyNamesContractResolver and provide the correct casing.
[JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
public record ExternalAuthInfo
{
    [Required]
    public required string ClientId { get; init; }

    [Required]
    public required string Code { get; init; }

    [Required]
    public required string RedirectUri { get; init; }

    public string? InviteToken { get; init; }
}
