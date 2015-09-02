using System;

namespace Exceptionless.Core.Models {
    public interface IOwnedByOrganizationAndProjectWithIdentity : IOwnedByOrganization, IOwnedByProject, IIdentity {
    }
}