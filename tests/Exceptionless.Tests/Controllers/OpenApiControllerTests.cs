using Exceptionless.Tests.Extensions;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public class OpenApiControllerTests : IntegrationTestsBase
{
    public OpenApiControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
    }

    [Fact]
    public async Task GetSwaggerJson_Default_ReturnsExpectedBaseline()
    {
        // Arrange
        string baselinePath = Path.Combine("..", "..", "..", "Controllers", "Data", "openapi.json");

        // Act
        var response = await SendRequestAsync(r => r
            .BaseUri(_server.BaseAddress)
            .AppendPaths("docs", "v2", "openapi.json")
            .StatusCodeShouldBeOk()
        );

        string actualJson = await response.Content.ReadAsStringAsync(TestCancellationToken);

        // Assert
        string expectedJson = await File.ReadAllTextAsync(baselinePath, TestCancellationToken);
        Assert.Equal(expectedJson, actualJson);
    }
}
