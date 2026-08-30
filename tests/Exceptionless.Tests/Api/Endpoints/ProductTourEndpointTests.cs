using Exceptionless.Core.Models;
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
    public async Task UpdateCurrentUserProductTourAsync_NewProgress_PersistsAndReturnsProgress()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();

        // Act
        var progress = await SendRequestAsAsync<ProductTourProgress>(request => request
            .Put()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "app-overview")
            .Content(new UpdateProductTourProgress { Status = ProductTourStatus.Dismissed, Version = 1 })
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(progress);
        Assert.Equal(ProductTourStatus.Dismissed, progress.Status);
        Assert.Equal(1, progress.Version);

        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, options => options.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(progress, persistedUser.ProductTours["app-overview"]);
    }

    [Fact]
    public async Task UpdateCurrentUserProductTourAsync_OlderProgress_PreservesStoredValue()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        currentUser.ProductTours[ProductTours.ExieOverview] = new ProductTourProgress
        {
            Status = ProductTourStatus.Completed,
            Version = 3
        };
        await _userRepository.SaveAsync(currentUser, options => options.Cache().ImmediateConsistency());

        // Act
        var replacement = await UpdateProgressAsync(ProductTours.ExieOverview, ProductTourStatus.Dismissed, 1);

        // Assert
        Assert.Equal(ProductTourStatus.Completed, replacement.Status);
        Assert.Equal(3, replacement.Version);
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, options => options.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(replacement, persistedUser.ProductTours["exie-overview"]);
    }

    [Fact]
    public async Task UpdateCurrentUserProductTourAsync_CompletedProgress_ReplacesDismissedProgressForSameVersion()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        await UpdateProgressAsync(ProductTours.ExieOverview, ProductTourStatus.Dismissed, 1);

        // Act
        var replacement = await UpdateProgressAsync(ProductTours.ExieOverview, ProductTourStatus.Completed, 1);

        // Assert
        Assert.Equal(ProductTourStatus.Completed, replacement.Status);
        Assert.Equal(1, replacement.Version);
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, options => options.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(replacement, persistedUser.ProductTours["exie-overview"]);
    }

    [Fact]
    public async Task UpdateCurrentUserProductTourAsync_ConcurrentUpdatesPreserveBothTourKeys()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();

        // Act
        await Task.WhenAll(
            UpdateProgressAsync(ProductTours.AppOverview, ProductTourStatus.Completed, 1),
            UpdateProgressAsync(ProductTours.SavedViewCreate, ProductTourStatus.Dismissed, 1));

        // Assert
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, options => options.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(ProductTourStatus.Completed, persistedUser.ProductTours[ProductTours.AppOverview].Status);
        Assert.Equal(ProductTourStatus.Dismissed, persistedUser.ProductTours[ProductTours.SavedViewCreate].Status);
    }

    [Fact]
    public async Task UpdateCurrentUserProductTourAsync_ConcurrentDismissAndCompleteLeavesCompletedProgress()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();

        // Act
        await Task.WhenAll(
            UpdateProgressAsync(ProductTours.ExieOverview, ProductTourStatus.Dismissed, 1),
            UpdateProgressAsync(ProductTours.ExieOverview, ProductTourStatus.Completed, 1));

        // Assert
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, options => options.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(ProductTourStatus.Completed, persistedUser.ProductTours[ProductTours.ExieOverview].Status);
    }

    [Fact]
    public async Task UpdateCurrentUserProductTourAsync_MissingUser_ReturnsNotFound()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        await _userRepository.RemoveAsync(currentUser.Id, options => options.ImmediateConsistency());

        // Act & Assert
        await SendRequestAsync(request => request
            .Put()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", ProductTours.AppOverview)
            .Content(new UpdateProductTourProgress { Status = ProductTourStatus.Completed, Version = 1 })
            .StatusCodeShouldBeNotFound());
    }

    [Fact]
    public async Task UpdateCurrentUserProductTourAsync_UnknownTourName_ReturnsUnprocessableEntity()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();

        // Act
        await SendRequestAsync(request => request
            .Put()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "unknown-tour")
            .Content(new UpdateProductTourProgress { Status = ProductTourStatus.Completed, Version = 1 })
            .StatusCodeShouldBeUnprocessableEntity());

        // Assert
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, options => options.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.DoesNotContain("unknown-tour", persistedUser.ProductTours);
    }

    [Fact]
    public Task UpdateCurrentUserProductTourAsync_InvalidTourName_DoesNotMatchRoute()
    {
        // Act & Assert
        return SendRequestAsync(request => request
            .Put()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "Invalid--Tour")
            .Content(new UpdateProductTourProgress { Status = ProductTourStatus.Completed, Version = 1 })
            .StatusCodeShouldBeNotFound());
    }

    [Fact]
    public Task UpdateCurrentUserProductTourAsync_AnonymousUser_ReturnsUnauthorized()
    {
        // Act & Assert
        return SendRequestAsync(request => request
            .Put()
            .AppendPaths("users", "me", "product-tours", ProductTours.AppWelcome)
            .Content(new UpdateProductTourProgress { Status = ProductTourStatus.Dismissed, Version = 1 })
            .StatusCodeShouldBeUnauthorized());
    }

    [Fact]
    public Task UpdateCurrentUserProductTourAsync_MissingBody_ReturnsBadRequest()
    {
        // Act & Assert
        return SendRequestAsync(request => request
            .Put()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "app-overview")
            .StatusCodeShouldBeBadRequest());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2)]
    public Task UpdateCurrentUserProductTourAsync_UnsupportedVersion_ReturnsUnprocessableEntity(int version)
    {
        // Act & Assert
        return SendRequestAsync(request => request
            .Put()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "app-overview")
            .Content(new UpdateProductTourProgress { Status = ProductTourStatus.Completed, Version = version })
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public Task UpdateCurrentUserProductTourAsync_UndefinedStatus_ReturnsUnprocessableEntity()
    {
        // Act & Assert
        return SendRequestAsync(request => request
            .Put()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "app-overview")
            .Content(new { Status = 999, Version = 1 })
            .StatusCodeShouldBeUnprocessableEntity());
    }

    private async Task<ProductTourProgress> UpdateProgressAsync(string tourName, ProductTourStatus status, int version)
    {
        var progress = await SendRequestAsAsync<ProductTourProgress>(request => request
            .Put()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", tourName)
            .Content(new UpdateProductTourProgress { Status = status, Version = version })
            .StatusCodeShouldBeOk());

        return Assert.IsType<ProductTourProgress>(progress);
    }

    private async Task<User> GetTestOrganizationUserAsync()
    {
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        return Assert.IsType<User>(user);
    }
}
