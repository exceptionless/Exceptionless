namespace Exceptionless.Web.Models.OAuth;

public record ViewOAuthGrant
{
    public required string Id { get; init; }
    public required string ClientId { get; init; }
    public required string ApplicationName { get; init; }
    public bool IsApplicationDisabled { get; init; }
    public required IReadOnlyCollection<string> Scopes { get; init; }
    public required IReadOnlyCollection<string> OrganizationIds { get; init; }
    public required IReadOnlyCollection<ViewOAuthGrantResource> Resources { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
    public DateTime? ExpiresUtc { get; init; }
    public DateTime? RefreshExpiresUtc { get; init; }
}

public record ViewOAuthGrantResource
{
    public required string Resource { get; init; }
    public required IReadOnlyCollection<string> Scopes { get; init; }
    public required IReadOnlyCollection<string> OrganizationIds { get; init; }
}
