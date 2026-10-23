using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Models;

public sealed class RemoveExternalLoginRequest
{
    [FromRoute(Name = "providerName")]
    [Required]
    [AllowedValues(OAuthAccount.KnownProviders.GitHub, OAuthAccount.KnownProviders.Google, OAuthAccount.KnownProviders.Microsoft,
        OAuthAccount.KnownProviders.Facebook, OAuthAccount.KnownProviders.WindowsLive,
        ErrorMessage = "The provider name must be github, google, microsoft, facebook, or windowslive.")]
    public string ProviderName { get; set; } = null!;

    [FromBody]
    public ValueFromBody<string> ProviderUserId { get; set; } = null!;
}
