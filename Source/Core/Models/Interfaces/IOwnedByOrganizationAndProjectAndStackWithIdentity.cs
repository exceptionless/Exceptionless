using System;

namespace Exceptionless.Core.Models {
    public interface IOwnedByOrganizationAndProjectAndStackWithIdentity : IOwnedByOrganizationAndProjectWithIdentity, IOwnedByStack {
    }
}