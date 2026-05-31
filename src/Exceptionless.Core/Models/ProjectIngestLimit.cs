namespace Exceptionless.Core.Models;

public class ProjectIngestLimit
{
    public ProjectIngestLimitType Type { get; set; }
    public int? FixedLimit { get; set; }
    public decimal? PercentOfOrganizationLimit { get; set; }
}

public enum ProjectIngestLimitType
{
    Fixed = 0,
    PercentOfOrganizationLimit = 1
}
