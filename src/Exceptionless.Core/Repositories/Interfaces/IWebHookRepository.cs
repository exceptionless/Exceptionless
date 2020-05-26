using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IWebHookRepository : IRepositoryOwnedByOrganizationAndProject<WebHook> {
        Task<QueryResults<WebHook>> GetByUrlAsync(string targetUrl);
        Task<QueryResults<WebHook>> GetByOrganizationIdOrProjectIdAsync(string organizationId, string projectId);
        Task MarkDisabledAsync(string id);
    }
}