using System;

namespace Exceptionless.Models {
    public interface IOwnedByOrganizationAndProjectAndStackWithIdentity : IOwnedByOrganizationAndProjectWithIdentity, IOwnedByStack {
    }
}