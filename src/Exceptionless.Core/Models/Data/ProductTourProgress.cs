namespace Exceptionless.Core.Models.Data;

public record ProductTourProgress
{
    public ProductTourStatus Status { get; set; }
    public int Version { get; set; }
}

public enum ProductTourStatus
{
    Completed = 1,
    Dismissed = 2
}
