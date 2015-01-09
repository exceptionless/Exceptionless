using System;

namespace Exceptionless.Models {
    public interface IOwnedByOrganizationAndProjectAndStack : IOwnedByOrganization, IOwnedByProject, IOwnedByStack {
    }
}
