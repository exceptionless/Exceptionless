using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Models;

public sealed class RemoveExternalLoginRequest : IValidatableObject
{
    [FromRoute(Name = "providerName")]
    [Required]
    [AllowedValues(OAuthAccount.KnownProviders.GitHub, OAuthAccount.KnownProviders.Google,
        OAuthAccount.KnownProviders.Facebook, OAuthAccount.KnownProviders.WindowsLive,
        ErrorMessage = "The provider name must be github, google, facebook, or windowslive.")]
    public string ProviderName { get; set; } = null!;

    [FromBody]
    public ValueFromBody<string> ProviderUserId { get; set; } = null!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (String.IsNullOrWhiteSpace(ProviderUserId?.Value))
            yield return new ValidationResult("The provider user id is required.", [nameof(ProviderUserId)]);
    }
}
