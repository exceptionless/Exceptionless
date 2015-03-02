using System;
using Exceptionless.Core.Models;

namespace Exceptionless.Api.Models {
    public class NewProject : UpdateProject, IOwnedByOrganization {   
        public string OrganizationId { get; set; }
    }
}