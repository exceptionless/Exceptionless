using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

public class WebHook : IOwnedByOrganizationAndProjectWithIdentity, IHaveCreatedDate
{
    public string Id { get; set; } = null!;
    public string OrganizationId { get; set; } = null!;
    public string ProjectId { get; set; } = null!;
    public string Url { get; set; } = null!;
    public string[] EventTypes { get; set; } = null!;

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// The schema version that should be used.
    /// </summary>
    public string Version { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }

    public static class KnownVersions
    {
        public const string Version1 = "v1";
        public const string Version2 = "v2";
    }
}
