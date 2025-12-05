using Exceptionless.Tests.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Controllers;

public class OpenApiControllerTests : IntegrationTestsBase
{
    public OpenApiControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
    }

    [Fact]
    public async Task GetSwaggerJson_ReturnsExpectedBaseline()
    {
        var response = await SendRequestAsync(r => r
            .BaseUri(_server.BaseAddress)
            .AppendPaths("docs", "v2", "swagger.json")
            .StatusCodeShouldBeOk()
        );

        string baselinePath = Path.Combine("..", "..", "..", "Controllers", "Data", "swagger.json");
        string expectedJson = await File.ReadAllTextAsync(baselinePath);
        string actualJson = await response.Content.ReadAsStringAsync();
        Assert.Equal(expectedJson, actualJson);
    }
}
