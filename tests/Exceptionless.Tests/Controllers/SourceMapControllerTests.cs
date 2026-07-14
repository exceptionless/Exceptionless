using System.Net;
using System.Text;
using Exceptionless.Core.Services.SourceMaps;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class SourceMapControllerTests : IntegrationTestsBase
{
    private const string GeneratedFileUrl = "https://cdn.example.com/assets/app.min.js";
    private static readonly byte[] SourceMap = Encoding.UTF8.GetBytes("""{"version":3,"sources":["src/app.ts"],"names":["meaningfulFunction"],"mappings":"AAAAA"}""");

    public SourceMapControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task PostAsync_WithValidSourceMap_CanListAndDeleteArtifact()
    {
        using var content = CreateSourceMapContent(SourceMap);

        var uploaded = await SendRequestAsAsync<SourceMapArtifact>(request => request
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "source-maps")
            .Content(content)
            .StatusCodeShouldBeCreated());

        Assert.NotNull(uploaded);
        Assert.Equal(GeneratedFileUrl, uploaded.GeneratedFileUrl);
        Assert.False(uploaded.IsAutoDownloaded);

        var artifacts = await SendRequestAsAsync<IReadOnlyCollection<SourceMapArtifact>>(request => request
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "source-maps")
            .StatusCodeShouldBeOk());
        Assert.NotNull(artifacts);
        Assert.Single(artifacts);

        await SendRequestAsync(request => request
            .Delete()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "source-maps", uploaded.Id)
            .StatusCodeShouldBeNoContent());
    }

    [Fact]
    public async Task PostAsync_WithInvalidSourceMap_ReturnsUnprocessableEntity()
    {
        using var content = CreateSourceMapContent(Encoding.UTF8.GetBytes("not a source map"));

        await SendRequestAsync(request => request
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "source-maps")
            .Content(content)
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public Task GetAsync_ProjectOutsideUserOrganization_ReturnsNotFound()
    {
        return SendRequestAsync(request => request
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.FREE_PROJECT_ID, "source-maps")
            .StatusCodeShouldBeNotFound());
    }

    private static MultipartFormDataContent CreateSourceMapContent(byte[] sourceMap)
    {
        var fileContent = new ByteArrayContent(sourceMap);
        fileContent.Headers.ContentType = new("application/json");

        var content = new MultipartFormDataContent();
        content.Add(new StringContent(GeneratedFileUrl), "generated_file_url");
        content.Add(fileContent, "file", "app.min.js.map");
        return content;
    }
}
