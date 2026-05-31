using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public record UpdateProject
{
    public string Name { get; set; } = null!;
    public bool DeleteBotDataEnabled { get; set; }
    public ProjectIngestLimit? IngestLimit { get; set; }
}
