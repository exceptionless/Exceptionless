using System;

namespace Exceptionless.Models {
    public interface IOwnedByOrganizationAndProject : IOwnedByOrganization, IOwnedByProject {
    }
}
