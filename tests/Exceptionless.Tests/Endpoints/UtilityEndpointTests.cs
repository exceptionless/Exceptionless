using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using FluentRest;
using Xunit;

namespace Exceptionless.Tests.Endpoints;

public class UtilityEndpointTests : IntegrationTestsBase
{
    public UtilityEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public Task ValidateAsync_WithEmptyQuery_ReturnsBadRequest()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("search/validate")
            .QueryString("query", String.Empty)
            .StatusCodeShouldBeBadRequest()
        );
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidQuery_ReturnsIsValidFalse()
    {
        // Arrange
        string query = "((unclosed";

        // Act
        var result = await SendRequestAsAsync<AppQueryValidator.QueryProcessResult>(r => r
            .AsGlobalAdminUser()
            .AppendPath("search/validate")
            .QueryString("query", query)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public Task ValidateAsync_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        string query = "type:error";

        // Act & Assert
        return SendRequestAsync(r => r
            .AppendPath("search/validate")
            .QueryString("query", query)
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task ValidateAsync_WithPremiumQuery_ReturnsPremiumFeaturesTrue()
    {
        // Arrange
        // `tags` is not in either validator's free-field list, so it triggers premium features
        string query = "tags:error";

        // Act
        var result = await SendRequestAsAsync<AppQueryValidator.QueryProcessResult>(r => r
            .AsGlobalAdminUser()
            .AppendPath("search/validate")
            .QueryString("query", query)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.True(result.UsesPremiumFeatures);
    }

    [Fact]
    public async Task ValidateAsync_WithValidSimpleQuery_ReturnsIsValidTrue()
    {
        // Arrange
        string query = "type:error";

        // Act
        var result = await SendRequestAsAsync<AppQueryValidator.QueryProcessResult>(r => r
            .AsGlobalAdminUser()
            .AppendPath("search/validate")
            .QueryString("query", query)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
    }
}
