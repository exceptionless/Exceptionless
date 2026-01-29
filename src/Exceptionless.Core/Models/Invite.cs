using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Core.Models;

public record Invite
{
    public required string Token { get; init; }

    [EmailAddress]
    public required string EmailAddress { get; init; }
    public required DateTime DateAdded { get; init; }
}
