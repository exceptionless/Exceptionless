using System.Text.Json;
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
    public async Task GetCurrentUserAsync_ReturnsAuthoritativeProductTourVersions()
    {
        var currentUser = await SendRequestAsAsync<JsonElement>(request => request
            .AsTestOrganizationUser()
            .AppendPaths("users", "me")
            .StatusCodeShouldBeOk());

        var versions = currentUser.GetProperty("product_tour_versions")
            .Deserialize<Dictionary<string, int>>();

        Assert.Equal(ProductTours.Versions, versions);
    }

    [Fact]
    public async Task UpdateCurrentUserProductTourAsync_OlderProgress_PreservesStoredValue()
    {
        var currentUser = await GetTestOrganizationUserAsync();
        currentUser.ProductTours[ProductTours.MeetExie] = new ProductTourProgress
        {
            Status = ProductTourStatus.Completed,
            UpdatedUtc = TimeProvider.GetUtcNow().UtcDateTime,
            Version = 3
        };
        await _userRepository.SaveAsync(currentUser, options => options.Cache().ImmediateConsistency());

        var replacement = await UpdateProgressAsync(ProductTours.MeetExie, ProductTourStatus.Dismissed, 1);

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
        await UpdateProgressAsync(ProductTours.MeetExie, ProductTourStatus.Dismissed, 1);

        var replacement = await UpdateProgressAsync(ProductTours.MeetExie, ProductTourStatus.Completed, 1);

        Assert.Equal(ProductTourStatus.Completed, replacement.Status);
        Assert.Equal(1, replacement.Version);
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, options => options.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(replacement, persistedUser.ProductTours["meet-exie"]);
    }

    [Fact]
    public async Task UpdateCurrentUserProductTourAsync_UnknownTourName_ReturnsUnprocessableEntity()
    {
        var currentUser = await GetTestOrganizationUserAsync();

        await SendRequestAsync(request => request
            .Put()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "unknown-tour")
            .Content(new UpdateProductTourProgress { Status = ProductTourStatus.Completed, Version = 1 })
            .StatusCodeShouldBeUnprocessableEntity());

        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, options => options.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.DoesNotContain("unknown-tour", persistedUser.ProductTours);
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
    [InlineData(2)]
    public Task UpdateCurrentUserProductTourAsync_UnsupportedVersion_ReturnsUnprocessableEntity(int version)
    {
        return SendRequestAsync(request => request
            .Put()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "ui-overview")
            .Content(new UpdateProductTourProgress { Status = ProductTourStatus.Completed, Version = version })
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public Task UpdateCurrentUserProductTourAsync_UndefinedStatus_ReturnsUnprocessableEntity()
    {
        return SendRequestAsync(request => request
            .Put()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "ui-overview")
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
