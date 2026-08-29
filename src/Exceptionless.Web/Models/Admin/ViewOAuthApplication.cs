using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models.Admin;

public record ViewOAuthApplication
{
    public required string Id { get; init; }
    public required string ClientId { get; init; }
    public required string Name { get; init; }
    public required string[] RedirectUris { get; init; }
    public required string[] Scopes { get; init; }
    public required IReadOnlyCollection<ViewOAuthApplicationOrganization> Organizations { get; init; }
    public string? Notes { get; init; }
    public bool IsDisabled { get; init; }
    public required string CreatedByUserId { get; init; }
    public string? UpdatedByUserId { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }

    public static ViewOAuthApplication FromApplication(OAuthApplication application, IReadOnlyDictionary<string, string>? organizationNames = null)
    {
        return new ViewOAuthApplication
        {
            Id = application.Id,
            ClientId = application.ClientId,
            Name = application.Name,
            RedirectUris = application.RedirectUris,
            Scopes = application.Scopes,
            Organizations = application.OrganizationIds
                .Select(id => new ViewOAuthApplicationOrganization(id, organizationNames?.GetValueOrDefault(id) ?? id))
                .OrderBy(organization => organization.Name)
                .ToArray(),
            Notes = application.Notes,
            IsDisabled = application.IsDisabled,
            CreatedByUserId = application.CreatedByUserId,
            UpdatedByUserId = application.UpdatedByUserId,
            CreatedUtc = application.CreatedUtc,
            UpdatedUtc = application.UpdatedUtc
        };
    }
}

public sealed record ViewOAuthApplicationOrganization(string Id, string Name);
