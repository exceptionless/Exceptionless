using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using FluentRest;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public class UtilityControllerTests : IntegrationTestsBase
{
    public UtilityControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task ValidateAsync_WithEmptyQuery_ReturnsResult()
    {
        // Arrange
        string query = "";

        // Act
        var result = await SendRequestAsAsync<AppQueryValidator.QueryProcessResult>(r => r
            .AsGlobalAdminUser()
            .AppendPath("search/validate")
            .QueryString("query", query)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(result);
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
        string query = "organization:123456789012345678901234";

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
