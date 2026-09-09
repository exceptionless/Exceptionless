using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using Foundatio.Repositories;
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
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        var utcNow = new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero);
        TimeProvider.SetUtcNow(utcNow);

        // Act
        var result = await SendRequestAsAsync<RecordProductTourResult>(r => r
            .Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTourNames.AppOverview, "record")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(utcNow.UtcDateTime, result.RecordedUtc);
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(utcNow.UtcDateTime, persistedUser.ProductTours.AppOverview);
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_RepeatedRequest_PreservesFirstTimestamp()
    {
        // Arrange
        await GetTestOrganizationUserAsync();
        var firstUtc = new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero);
        TimeProvider.SetUtcNow(firstUtc);
        var first = await SendRequestAsAsync<RecordProductTourResult>(r => r
            .Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTourNames.SavedViewCreate, "record")
            .StatusCodeShouldBeOk());

        // Act
        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        var second = await SendRequestAsAsync<RecordProductTourResult>(r => r
            .Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTourNames.SavedViewCreate, "record")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.RecordedUtc, second.RecordedUtc);
        Assert.Equal(firstUtc.UtcDateTime, second.RecordedUtc);
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_IgnoresClientTimestampAndPath()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        var serverUtc = new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero);
        TimeProvider.SetUtcNow(serverUtc);

        // Act
        var result = await SendRequestAsAsync<RecordProductTourResult>(r => r
            .Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTourNames.AppOverview, "record")
            .Content(new { recorded_utc = "2000-01-01T00:00:00Z", field = "full_name" })
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(serverUtc.UtcDateTime, result.RecordedUtc);
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(currentUser.FullName, persistedUser.FullName);
        Assert.Equal(serverUtc.UtcDateTime, persistedUser.ProductTours.AppOverview);
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_SequentialTours_PreservesBothDates()
    {
        // Arrange
        await GetTestOrganizationUserAsync();

        // Act
        await SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTourNames.AppOverview, "record")
            .StatusCodeShouldBeOk());
        await SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTourNames.ExieOverview, "record")
            .StatusCodeShouldBeOk());

        // Assert
        var currentUser = await GetTestOrganizationUserAsync();
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.NotNull(persistedUser.ProductTours.AppOverview);
        Assert.NotNull(persistedUser.ProductTours.ExieOverview);
    }

    [Fact]
    public Task RecordCurrentUserProductTourAsync_UnknownTour_ReturnsUnprocessableEntity()
    {
        // Arrange: sample users are created by ResetDataAsync.

        // Act & Assert
        return SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "unknown-tour", "record")
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public Task RecordCurrentUserProductTourAsync_OldRoute_ReturnsNotFound()
    {
        // Arrange: sample users are created by ResetDataAsync.

        // Act & Assert
        return SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTourNames.AppOverview)
            .StatusCodeShouldBeNotFound());
    }

    [Fact]
    public Task RecordCurrentUserProductTourAsync_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange: sample users are created by ResetDataAsync.

        // Act & Assert
        return SendRequestAsync(r => r.Put()
            .AppendPaths("users", "me", "product-tours", ProductTourNames.AppOverview, "record")
            .StatusCodeShouldBeUnauthorized());
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_DeletedUserReturnsUnauthorizedAndDoesNotCreate()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        await _userRepository.RemoveAsync(currentUser.Id, o => o.ImmediateConsistency());

        // Act
        await SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTourNames.AppOverview, "record")
            .StatusCodeShouldBeUnauthorized());

        // Assert
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
