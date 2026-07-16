using System.Net;
using System.Text;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services.SourceMaps;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class SourceMapEndpointTests : IntegrationTestsBase
{
    private const string GeneratedFileUrl = "https://cdn.example.com/assets/app.min.js";
    private static readonly byte[] SourceMap = Encoding.UTF8.GetBytes("""{"version":3,"sources":["src/app.ts"],"names":["meaningfulFunction"],"mappings":"AAAAA"}""");

    public SourceMapEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
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
    public async Task PostAsync_WithMissingMappings_ReturnsUnprocessableEntity()
    {
        using var content = CreateSourceMapContent(Encoding.UTF8.GetBytes("""{"version":3,"sources":[],"names":[]}"""));

        await SendRequestAsync(request => request
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "source-maps")
            .Content(content)
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public async Task PostAsync_WithOversizedVersion_ReturnsUnprocessableEntity()
    {
        using var content = CreateSourceMapContent(Encoding.UTF8.GetBytes("""{"version":999999999999,"sources":[],"names":[],"mappings":""}"""));

        await SendRequestAsync(request => request
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "source-maps")
            .Content(content)
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public async Task PostAsync_WithoutMultipartContent_ReturnsUnprocessableEntity()
    {
        using var content = new StringContent("not multipart");

        await SendRequestAsync(request => request
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "source-maps")
            .Content(content)
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public async Task PostAsync_WithMalformedMultipartContent_ReturnsUnprocessableEntity()
    {
        using var content = new StringContent("not multipart");
        content.Headers.ContentType = new("multipart/form-data");

        await SendRequestAsync(request => request
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "source-maps")
            .Content(content)
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public async Task PostAsync_WithProjectSourceMapsWriteToken_CreatesArtifact()
    {
        var token = await CreateSourceMapUploadTokenAsync(SampleDataService.TEST_PROJECT_ID);
        using var content = CreateSourceMapContent(SourceMap);

        var uploaded = await SendRequestAsAsync<SourceMapArtifact>(request => request
            .Post()
            .BearerToken(token.Id)
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "source-maps")
            .Content(content)
            .StatusCodeShouldBeCreated());

        Assert.NotNull(uploaded);
        Assert.Equal(GeneratedFileUrl, uploaded.GeneratedFileUrl);
    }

    [Fact]
    public async Task PostAsync_WithClientToken_ReturnsForbidden()
    {
        using var content = CreateSourceMapContent(SourceMap);

        await SendRequestAsync(request => request
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "source-maps")
            .Content(content)
            .StatusCodeShouldBeForbidden());
    }

    [Fact]
    public async Task PostAsync_WithSourceMapsWriteTokenForDifferentProject_ReturnsNotFound()
    {
        var otherProject = await GetService<IProjectRepository>().AddAsync(new Project
        {
            Name = "Other project in the same organization",
            OrganizationId = SampleDataService.TEST_ORG_ID,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks
        }, options => options.ImmediateConsistency());
        var token = await CreateSourceMapUploadTokenAsync(SampleDataService.TEST_PROJECT_ID);
        using var content = CreateSourceMapContent(SourceMap);

        await SendRequestAsync(request => request
            .Post()
            .BearerToken(token.Id)
            .AppendPaths("projects", otherProject.Id, "source-maps")
            .Content(content)
            .StatusCodeShouldBeNotFound());
    }

    [Fact]
    public async Task PostAsync_WithOrganizationScopedSourceMapsWriteToken_ReturnsNotFound()
    {
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var token = await GetService<ITokenRepository>().AddAsync(new Token
        {
            Id = StringExtensions.GetNewToken(),
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Scopes = [AuthorizationRoles.SourceMapsWrite],
            Type = TokenType.Access,
            CreatedBy = TestConstants.UserId,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        }, options => options.ImmediateConsistency());
        using var content = CreateSourceMapContent(SourceMap);

        await SendRequestAsync(request => request
            .Post()
            .BearerToken(token.Id)
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "source-maps")
            .Content(content)
            .StatusCodeShouldBeNotFound());
    }

    [Fact]
    public async Task GetAsync_WithSourceMapsWriteToken_ReturnsForbidden()
    {
        var token = await CreateSourceMapUploadTokenAsync(SampleDataService.TEST_PROJECT_ID);

        await SendRequestAsync(request => request
            .BearerToken(token.Id)
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "source-maps")
            .StatusCodeShouldBeForbidden());
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

    private async Task<ViewToken> CreateSourceMapUploadTokenAsync(string projectId)
    {
        var token = await SendRequestAsAsync<ViewToken>(request => request
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("projects", projectId, "tokens")
            .Content(new NewToken
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                ProjectId = projectId,
                Scopes = [AuthorizationRoles.SourceMapsWrite],
                Notes = "Source map deployment"
            })
            .StatusCodeShouldBeCreated());

        Assert.NotNull(token);
        await RefreshDataAsync();
        return token;
    }
}
