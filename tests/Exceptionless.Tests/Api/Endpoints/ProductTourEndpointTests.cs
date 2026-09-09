using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Repositories.Exceptions;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class ProductTourEndpointTests : IntegrationTestsBase
{
    private readonly IUserRepository _userRepository;

    public ProductTourEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _userRepository = GetService<IUserRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_NewTour_ReturnsAndPersistsServerTimestamp()
    {
        var currentUser = await GetTestOrganizationUserAsync();
        var utcNow = new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero);
        TimeProvider.SetUtcNow(utcNow);

        var result = await SendRequestAsAsync<RecordProductTourResult>(r => r
            .Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTours.AppOverview, "record")
            .StatusCodeShouldBeOk());

        Assert.NotNull(result);
        Assert.Equal(utcNow.UtcDateTime, result.RecordedUtc);
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(utcNow.UtcDateTime, persistedUser.ProductTours.AppOverview);
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_RepeatedRequest_PreservesFirstTimestamp()
    {
        await GetTestOrganizationUserAsync();
        var firstUtc = new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero);
        TimeProvider.SetUtcNow(firstUtc);
        var first = await SendRequestAsAsync<RecordProductTourResult>(r => r
            .Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTours.SavedViewCreate, "record")
            .StatusCodeShouldBeOk());

        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        var second = await SendRequestAsAsync<RecordProductTourResult>(r => r
            .Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTours.SavedViewCreate, "record")
            .StatusCodeShouldBeOk());

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.RecordedUtc, second.RecordedUtc);
        Assert.Equal(firstUtc.UtcDateTime, second.RecordedUtc);
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_IgnoresClientTimestampAndPath()
    {
        var currentUser = await GetTestOrganizationUserAsync();
        var serverUtc = new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero);
        TimeProvider.SetUtcNow(serverUtc);

        var result = await SendRequestAsAsync<RecordProductTourResult>(r => r
            .Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTours.AppOverview, "record")
            .Content(new { recorded_utc = "2000-01-01T00:00:00Z", field = "full_name" })
            .StatusCodeShouldBeOk());

        Assert.NotNull(result);
        Assert.Equal(serverUtc.UtcDateTime, result.RecordedUtc);
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(currentUser.FullName, persistedUser.FullName);
        Assert.Equal(serverUtc.UtcDateTime, persistedUser.ProductTours.AppOverview);
    }

    [Fact]
    public async Task RecordProductTourAsync_CachedUser_RefreshesIdAndEmailCaches()
    {
        var user = await GetTestOrganizationUserAsync();
        await _userRepository.GetByIdAsync(user.Id, options => options.Cache());
        await _userRepository.GetByEmailAddressAsync(user.EmailAddress);
        var recordedUtc = new DateTime(2026, 9, 8, 20, 0, 0, DateTimeKind.Utc);

        var state = await _userRepository.RecordProductTourAsync(user.Id, "app_overview", recordedUtc);

        var cache = Assert.IsType<InMemoryCacheClient>(GetService<ICacheClient>());
        long hits = cache.Hits;
        long misses = cache.Misses;
        var cachedUser = await _userRepository.GetByIdAsync(user.Id, options => options.Cache());
        var cachedByEmail = await _userRepository.GetByEmailAddressAsync(user.EmailAddress);
        Assert.Equal(misses, cache.Misses);
        Assert.Equal(hits + 2, cache.Hits);
        Assert.Equal(recordedUtc, state.AppOverview);
        Assert.Equal(recordedUtc, cachedUser?.ProductTours.AppOverview);
        Assert.Equal(recordedUtc, cachedByEmail?.ProductTours.AppOverview);
    }

    [Fact]
    public async Task RecordProductTourAsync_CacheRepopulatedAfterPatch_ReturnsLatestState()
    {
        var user = await GetTestOrganizationUserAsync();
        await _userRepository.GetByIdAsync(user.Id, options => options.Cache());
        var cache = GetService<ICacheClient>();
        string cacheKey = $"User:{user.Id}";
        var staleEntry = await cache.GetAsync<ICollection<FindHit<User>>>(cacheKey);
        Assert.True(staleEntry.HasValue);
        var repository = Assert.IsType<UserRepository>(_userRepository);

        using var subscription = repository.BeforeGet.AddHandler(async (_, _) =>
        {
            Assert.False((await cache.GetAsync<ICollection<FindHit<User>>>(cacheKey)).HasValue);
            await cache.SetAsync(cacheKey, staleEntry.Value);
        });

        var recordedUtc = new DateTime(2026, 9, 8, 20, 0, 0, DateTimeKind.Utc);
        var state = await _userRepository.RecordProductTourAsync(user.Id, "app_overview", recordedUtc);

        Assert.Equal(recordedUtc, state.AppOverview);
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_ConcurrentTours_PreservesBothDates()
    {
        await GetTestOrganizationUserAsync();
        Task first = SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTours.AppOverview, "record")
            .StatusCodeShouldBeOk());
        Task second = SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTours.ExieOverview, "record")
            .StatusCodeShouldBeOk());

        await Task.WhenAll(first, second);
        var currentUser = await GetTestOrganizationUserAsync();
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.NotNull(persistedUser.ProductTours.AppOverview);
        Assert.NotNull(persistedUser.ProductTours.ExieOverview);
    }

    [Fact]
    public Task RecordCurrentUserProductTourAsync_UnknownTour_ReturnsUnprocessableEntity() =>
        SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "unknown-tour", "record")
            .StatusCodeShouldBeUnprocessableEntity());

    [Fact]
    public Task RecordCurrentUserProductTourAsync_OldRoute_ReturnsNotFound() =>
        SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTours.AppOverview)
            .StatusCodeShouldBeNotFound());

    [Fact]
    public Task RecordCurrentUserProductTourAsync_AnonymousUser_ReturnsUnauthorized() =>
        SendRequestAsync(r => r.Put()
            .AppendPaths("users", "me", "product-tours", ProductTours.AppOverview, "record")
            .StatusCodeShouldBeUnauthorized());

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_DeletedUserReturnsUnauthorizedAndRepositoryDoesNotCreate()
    {
        var currentUser = await GetTestOrganizationUserAsync();
        await _userRepository.RemoveAsync(currentUser.Id, o => o.ImmediateConsistency());

        await SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTours.AppOverview, "record")
            .StatusCodeShouldBeUnauthorized());

        await Assert.ThrowsAsync<DocumentNotFoundException>(() =>
            _userRepository.RecordProductTourAsync(currentUser.Id, "app_overview", TimeProvider.GetUtcNow().UtcDateTime));

        Assert.Null(await _userRepository.GetByIdAsync(currentUser.Id, o => o.Cache(false)));
    }

    private async Task<ViewUser> GetTestOrganizationUserAsync()
    {
        var user = await SendRequestAsAsync<ViewUser>(r => r
            .AsTestOrganizationUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk());
        Assert.NotNull(user);
        return user;
    }
}
