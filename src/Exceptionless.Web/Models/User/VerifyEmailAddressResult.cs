namespace Exceptionless.Web.Models;

/// <summary>
/// The account whose email address the link verified, which may not be the account that's signed in.
/// </summary>
public record VerifyEmailAddressResult
{
    public required string EmailAddress { get; init; }
}
