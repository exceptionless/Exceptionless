namespace Exceptionless.Web.Models;

public record UpdateEmailAddressResult
{
    public required bool IsVerified { get; init; }
}
