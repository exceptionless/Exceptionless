using System;

namespace Exceptionless.Models {
    public interface IOwnedByOrganizationWithIdentity : IOwnedByOrganization, IIdentity {}
}