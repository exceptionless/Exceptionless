using System.Net;
using System.Text;
using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.EventProcessor.Default;
using Exceptionless.Core.Services.SourceMaps;
using Foundatio.Caching;
using Foundatio.Serializer;
using Foundatio.Storage;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Exceptionless.Tests.Services.SourceMaps;

public sealed class SourceMapServiceTests : TestWithServices
{
    private const string ProjectId = "507f1f77bcf86cd799439011";
    private const string GeneratedFileUrl = "https://cdn.example.com/assets/app.min.js";
    private static readonly byte[] SourceMap = Encoding.UTF8.GetBytes("""{"version":3,"sources":["src/app.ts"],"names":["meaningfulFunction"],"mappings":"AAAAA"}""");

    public SourceMapServiceTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task SymbolicateAsync_WithUploadedSourceMap_RewritesFrameAndPreservesGeneratedLocation()
    {
        var service = GetService<SourceMapService>();
        await using var sourceMapStream = new MemoryStream(SourceMap);
        var artifact = await service.SaveUploadedAsync(ProjectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);
        var error = CreateError();

        bool changed = await service.SymbolicateAsync(ProjectId, error, TestContext.Current.CancellationToken);

        Assert.True(changed);
        var frame = Assert.Single(error.StackTrace!);
        Assert.Equal("meaningfulFunction", frame.Name);
        Assert.Equal("src/app.ts", frame.FileName);
        Assert.Equal(1, frame.LineNumber);
        Assert.Equal(1, frame.Column);
        var generatedLocation = Assert.IsType<DataDictionary>(frame.Data!["@source_map"]);
        Assert.Equal(GeneratedFileUrl, generatedLocation["generated_file_name"]);
        Assert.Equal(artifact.Id, generatedLocation["source_map_id"]);
    }

    [Fact]
    public async Task GetArtifactsAsync_AfterUploadAndDelete_ReflectsStoredArtifact()
    {
        var service = GetService<SourceMapService>();
        await using var sourceMapStream = new MemoryStream(SourceMap);
        var uploaded = await service.SaveUploadedAsync(ProjectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);

        var artifacts = await service.GetArtifactsAsync(ProjectId, TestContext.Current.CancellationToken);
        bool deleted = await service.DeleteArtifactAsync(ProjectId, uploaded.Id, TestContext.Current.CancellationToken);

        var artifact = Assert.Single(artifacts);
        Assert.Equal(GeneratedFileUrl, artifact.GeneratedFileUrl);
        Assert.False(artifact.IsAutoDownloaded);
        Assert.True(deleted);
        Assert.Empty(await service.GetArtifactsAsync(ProjectId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SymbolicateAsync_WithPublicSourceMapHeader_DownloadsAndStoresSourceMap()
    {
        var requestedUris = new List<Uri>();
        var handler = new DelegateHandler(request =>
        {
            requestedUris.Add(request.RequestUri!);
            if (request.RequestUri == new Uri(GeneratedFileUrl))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("minified") };
                response.Headers.TryAddWithoutValidation("SourceMap", "app.min.js.map");
                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(SourceMap) };
        });
        using var httpClient = new HttpClient(handler);
        var service = new SourceMapService(
            new TestHttpClientFactory(httpClient),
            GetService<IFileStorage>(),
            GetService<ICacheClient>(),
            GetService<JsonSerializerOptions>(),
            GetService<AppOptions>(),
            GetService<TimeProvider>(),
            GetService<ILogger<SourceMapService>>());
        var error = CreateError();

        bool changed = await service.SymbolicateAsync(ProjectId, error, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal([new Uri(GeneratedFileUrl), new Uri("https://cdn.example.com/assets/app.min.js.map")], requestedUris);
        var artifact = Assert.Single(await service.GetArtifactsAsync(ProjectId, TestContext.Current.CancellationToken));
        Assert.True(artifact.IsAutoDownloaded);
        Assert.Equal("https://cdn.example.com/assets/app.min.js.map", artifact.SourceMapUrl);
    }

    [Fact]
    public async Task EventProcessingAsync_WithUploadedSourceMap_UsesOriginalNameForStackSignature()
    {
        var service = GetService<SourceMapService>();
        await using var sourceMapStream = new MemoryStream(SourceMap);
        await service.SaveUploadedAsync(ProjectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);
        var serializer = GetService<ITextSerializer>();
        var options = GetService<AppOptions>();
        var loggerFactory = GetService<ILoggerFactory>();
        var sourceMapPlugin = new SourceMapPlugin(service, serializer, options, loggerFactory);
        var errorPlugin = new ErrorPlugin(serializer, options, loggerFactory);
        var persistentEvent = new PersistentEvent { Type = Event.KnownTypes.Error };
        persistentEvent.SetError(CreateError());
        var context = new EventContext(
            persistentEvent,
            new Organization { Id = "507f1f77bcf86cd799439012" },
            new Project { Id = ProjectId, OrganizationId = "507f1f77bcf86cd799439012" });

        await sourceMapPlugin.EventProcessingAsync(context);
        await errorPlugin.EventProcessingAsync(context);

        Assert.Equal("meaningfulFunction()", context.StackSignatureData["Method"]);
        Assert.True(sourceMapPlugin.HandleError(new IOException("Unavailable"), context));
    }

    private static Error CreateError()
    {
        return new Error
        {
            Message = "Test error",
            Type = "TypeError",
            StackTrace =
            [
                new StackFrame
                {
                    Name = "a",
                    FileName = GeneratedFileUrl,
                    LineNumber = 1,
                    Column = 1
                }
            ]
        };
    }

    private sealed class TestHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
