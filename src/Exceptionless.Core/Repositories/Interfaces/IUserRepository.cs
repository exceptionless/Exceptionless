using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public interface IUserRepository : ISearchableRepository<User>
{
    Task<bool> SetSavedViewOrdersAsync(User user, CommandOptionsDescriptor<User>? options = null);
    Task<User?> GetByEmailAddressAsync(string emailAddress);
    Task<User?> GetByPasswordResetTokenAsync(string token);
    Task<User?> GetUserByOAuthProviderAsync(string provider, string providerUserId);
    Task<User?> GetByVerifyEmailAddressTokenAsync(string token);
    Task<FindResults<User>> GetByOrganizationIdAsync(string organizationId, CommandOptionsDescriptor<User>? options = null);
    Task<ProductTourProgress> UpdateProductTourProgressAsync(string userId, string tourName, ProductTourProgress progress);
}
