namespace Exceptionless.Core.Models.Data;

public sealed record ProductTourState
{
    public DateTime? AppOverview { get; set; }
    public DateTime? ExieOverview { get; set; }
    public DateTime? EventInvestigate { get; set; }
    public DateTime? ProjectConfigure { get; set; }
    public DateTime? SavedViewCreate { get; set; }
    public DateTime? AppWelcome { get; set; }
    public DateTime? ExieAnnouncement { get; set; }
}
