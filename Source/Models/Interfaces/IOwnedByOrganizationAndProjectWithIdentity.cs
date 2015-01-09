using System;

namespace Exceptionless.Models {
    public interface IOwnedByOrganizationAndProjectWithIdentity : IOwnedByOrganization, IOwnedByProject, IIdentity {
    }
}