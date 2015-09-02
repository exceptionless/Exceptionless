using System;

namespace Exceptionless.Core.Models {
    public interface IOwnedByOrganizationAndProject : IOwnedByOrganization, IOwnedByProject {
    }
}
