using System;
using Exceptionless.Models;

namespace Exceptionless.Api.Models {
    public class NewProject : UpdateProject, IOwnedByOrganization {   
        public string OrganizationId { get; set; }
    }
}