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
        var currentUser = await GetTestOrganizationUserAsync();

        var progress = await SendRequestAsAsync<ProductTourProgress>(request => request
            .Put()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "ui-overview")
            .Content(new UpdateProductTourProgress { Status = ProductTourStatus.Dismissed, Version = 1 })
            .StatusCodeShouldBeOk());

        Assert.NotNull(progress);
        Assert.Equal(ProductTourStatus.Dismissed, progress.Status);
        Assert.Equal(1, progress.Version);

        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, options => options.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(progress, persistedUser.ProductTours["ui-overview"]);
    }

    [Fact]
    public async Task UpdateCurrentUserProductTourAsync_OlderProgress_PreservesStoredValue()
    {
        var currentUser = await GetTestOrganizationUserAsync();
        await UpdateProgressAsync("meet-exie", ProductTourStatus.Completed, 3);

        var replacement = await UpdateProgressAsync("meet-exie", ProductTourStatus.Dismissed, 1);

        Assert.Equal(ProductTourStatus.Completed, replacement.Status);
        Assert.Equal(3, replacement.Version);
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, options => options.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(replacement, persistedUser.ProductTours["meet-exie"]);
    }

    [Fact]
    public async Task UpdateCurrentUserProductTourAsync_CompletedProgress_ReplacesDismissedProgressForSameVersion()
    {
        var currentUser = await GetTestOrganizationUserAsync();
        await UpdateProgressAsync("meet-exie", ProductTourStatus.Dismissed, 3);

        var replacement = await UpdateProgressAsync("meet-exie", ProductTourStatus.Completed, 3);

        Assert.Equal(ProductTourStatus.Completed, replacement.Status);
        Assert.Equal(3, replacement.Version);
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, options => options.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(replacement, persistedUser.ProductTours["meet-exie"]);
    }

    [Fact]
    public async Task UpdateCurrentUserProductTourAsync_MoreThanThirtyTwoNames_ReturnsUnprocessableEntity()
    {
        var currentUser = await GetTestOrganizationUserAsync();
        var user = await _userRepository.GetByIdAsync(currentUser.Id, options => options.Cache(false));
        Assert.NotNull(user);
        foreach (int index in Enumerable.Range(0, 32))
        {
            user.ProductTours[$"guide-{index}"] = new ProductTourProgress
            {
                Status = ProductTourStatus.Dismissed,
                UpdatedUtc = TimeProvider.GetUtcNow().UtcDateTime,
                Version = 1
            };
        }
        await _userRepository.SaveAsync(user, options => options.Cache().ImmediateConsistency());

        await SendRequestAsync(request => request
            .Put()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "guide-32")
            .Content(new UpdateProductTourProgress { Status = ProductTourStatus.Completed, Version = 1 })
            .StatusCodeShouldBeUnprocessableEntity());

        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, options => options.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(32, persistedUser.ProductTours.Count);
        Assert.DoesNotContain("guide-32", persistedUser.ProductTours);
    }

    [Fact]
    public Task UpdateCurrentUserProductTourAsync_InvalidTourName_DoesNotMatchRoute()
    {
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
        return SendRequestAsync(request => request
            .Put()
            .AppendPaths("users", "me", "product-tours", "welcome")
            .Content(new UpdateProductTourProgress { Status = ProductTourStatus.Dismissed, Version = 1 })
            .StatusCodeShouldBeUnauthorized());
    }

    [Fact]
    public Task UpdateCurrentUserProductTourAsync_MissingBody_ReturnsBadRequest()
    {
        return SendRequestAsync(request => request
            .Put()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "ui-overview")
            .StatusCodeShouldBeBadRequest());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public Task UpdateCurrentUserProductTourAsync_NonPositiveVersion_ReturnsUnprocessableEntity(int version)
    {
        return SendRequestAsync(request => request
            .Put()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "invalid-version")
            .Content(new UpdateProductTourProgress { Status = ProductTourStatus.Completed, Version = version })
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public Task UpdateCurrentUserProductTourAsync_UndefinedStatus_ReturnsUnprocessableEntity()
    {
        return SendRequestAsync(request => request
            .Put()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "invalid-status")
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
