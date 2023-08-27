namespace Exceptionless.Core.Models;

public record Invite
{
    public required string Token { get; init; }
    public required string EmailAddress { get; init; }
    public required DateTime DateAdded { get; init; }
}
