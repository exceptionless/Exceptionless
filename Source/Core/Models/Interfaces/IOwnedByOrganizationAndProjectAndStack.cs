using System;

namespace Exceptionless.Core.Models {
    public interface IOwnedByOrganizationAndProjectAndStack : IOwnedByOrganization, IOwnedByProject, IOwnedByStack {
    }
}
