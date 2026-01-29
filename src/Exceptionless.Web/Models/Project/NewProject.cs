using Exceptionless.Core.Attributes;
using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public record NewProject : UpdateProject, IOwnedByOrganization
{
    [ObjectId]
    public string OrganizationId { get; set; } = null!;
}
