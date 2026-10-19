using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Models;

public sealed class RemoveExternalLoginRequest
{
    [FromRoute(Name = "providerName")]
    [Required]
    [AllowedValues(OAuthAccount.KnownProviders.GitHub, OAuthAccount.KnownProviders.Google,
        OAuthAccount.KnownProviders.Facebook, OAuthAccount.KnownProviders.WindowsLive,
        ErrorMessage = "The provider name must be github, google, facebook, or windowslive.")]
    public string ProviderName { get; set; } = null!;
}
