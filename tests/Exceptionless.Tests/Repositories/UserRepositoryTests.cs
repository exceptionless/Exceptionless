using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Repositories;

public sealed class UserRepositoryTests : IntegrationTestsBase
{
    private readonly ICacheClient _cache;
    private readonly IUserRepository _repository;

    public UserRepositoryTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _cache = GetService<ICacheClient>();
        _repository = GetService<IUserRepository>();
    }

    [Fact]
    public async Task CanSaveCachedVersionedUserAsync()
    {
        var user = new User
        {
            FullName = "Cached User",
            EmailAddress = $"cached-{Guid.NewGuid():N}@localhost.com",
            IsEmailAddressVerified = true
        };
        await _repository.AddAsync(user, o => o.ImmediateConsistency());
        Assert.False(String.IsNullOrEmpty(user.Version));

        await _cache.RemoveAllAsync();
        await _repository.GetByIdAsync(user.Id, o => o.Cache());
        var cachedUser = await _repository.GetByIdAsync(user.Id, o => o.Cache());
        Assert.NotNull(cachedUser);
        Assert.False(String.IsNullOrEmpty(cachedUser.Version));

        cachedUser.FullName = "Updated Cached User";
        await _repository.SaveAsync(cachedUser, o => o.ImmediateConsistency().Cache());

        var persistedUser = await _repository.GetByIdAsync(user.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal("Updated Cached User", persistedUser.FullName);
    }

    [Fact]
    public async Task AddOrganizationAsync_PreservesConcurrentUserChanges()
    {
        var user = new User
        {
            FullName = "Organization Member",
            EmailAddress = $"organization-member-{Guid.NewGuid():N}@localhost.com",
            IsEmailAddressVerified = true
        };
        await _repository.AddAsync(user, o => o.ImmediateConsistency());

        string preferenceOrganizationId = ObjectId.GenerateNewId().ToString();
        string savedViewId = ObjectId.GenerateNewId().ToString();
        string newOrganizationId = ObjectId.GenerateNewId().ToString();
        await _repository.SetDefaultSavedViewAsync(user.Id, preferenceOrganizationId, savedViewId);

        Assert.True(await _repository.AddOrganizationAsync(user.Id, newOrganizationId));

        var persistedUser = await _repository.GetByIdAsync(user.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Contains(newOrganizationId, persistedUser.OrganizationIds);
        Assert.Contains(persistedUser.OrganizationPreferences, preference =>
            preference.OrganizationId == preferenceOrganizationId && preference.DefaultSavedViewId == savedViewId);
    }
}
