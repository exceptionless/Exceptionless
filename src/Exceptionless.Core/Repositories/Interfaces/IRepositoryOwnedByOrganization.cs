using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IRepositoryOwnedByOrganization<T> : IQueryableRepository<T> where T : class, IOwnedByOrganization, IIdentity, new() {
        Task<QueryResults<T>> GetByOrganizationIdAsync(string organizationId, CommandOptionsDescriptor<T> options = null);
        Task<long> RemoveAllByOrganizationIdAsync(string organizationId);
    }
}
