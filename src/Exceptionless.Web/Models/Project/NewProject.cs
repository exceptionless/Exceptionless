using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models {
    public class NewProject : UpdateProject, IOwnedByOrganization {   
        public string OrganizationId { get; set; }
    }
}