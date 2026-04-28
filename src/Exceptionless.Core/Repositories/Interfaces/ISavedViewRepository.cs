using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public interface ISavedViewRepository : IRepositoryOwnedByOrganization<SavedView>
{
    Task<FindResults<SavedView>> GetByViewAsync(string organizationId, string viewType, CommandOptionsDescriptor<SavedView>? options = null);
    Task<FindResults<SavedView>> GetByViewForUserAsync(string organizationId, string viewType, string userId, CommandOptionsDescriptor<SavedView>? options = null);
    Task<FindResults<SavedView>> GetByOrganizationForUserAsync(string organizationId, string userId, CommandOptionsDescriptor<SavedView>? options = null);
    Task<long> CountByOrganizationIdAsync(string organizationId);

    /// <summary>Removes all private saved views belonging to a specific user within an organization.</summary>
    Task<long> RemovePrivateByUserIdAsync(string organizationId, string userId);
}
