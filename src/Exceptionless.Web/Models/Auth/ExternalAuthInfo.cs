namespace Exceptionless.Web.Models;

public record ExternalAuthInfo
{
    public required string ClientId { get; set; }
    public required string Code { get; set; }
    public required string RedirectUri { get; set; }
    public required string InviteToken { get; set; }
}
