using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Queues.Models;

public class EventUserDescription : UserDescription
{
    public required string ReferenceId { get; set; }
    public required string ProjectId { get; set; }
}
