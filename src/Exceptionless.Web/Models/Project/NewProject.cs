using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public record NewProject : UpdateProject, IOwnedByOrganization
{
    public string OrganizationId { get; set; } = null!;
}
